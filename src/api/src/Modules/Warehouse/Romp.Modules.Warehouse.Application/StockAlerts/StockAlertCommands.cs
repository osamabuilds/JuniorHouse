using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain.StockAlerts;

namespace Romp.Modules.Warehouse.Application.StockAlerts;

public sealed record AlertRowDto(
    long Id, long VariantRefId, string SkuCode, string Gtin, short LevelId, string LevelCode, int Available, int Threshold,
    DateTimeOffset OpenedAt, bool Acknowledged, string? AcknowledgedBy, DateTimeOffset? AcknowledgedAt);

public sealed record ThresholdRowDto(long VariantRefId, string SkuCode, string Gtin, int Available, int EffectiveThreshold, bool IsOverride);

public sealed record OpenAlertCountDto(int Open, int Unacknowledged);

/// <summary>
/// Evaluates one variant's alert against its current stock (ALR-1..4, ALR-6): opens, changes level, observes or closes, never creating
/// a second open alert. It re-reads the available quantity from the ledger rather than trusting an event's number, so a redelivered or
/// out-of-order event converges on the right state. Used by the outbox consumer and by threshold changes.
/// </summary>
public sealed class AlertEvaluator(IWarehouseDbContext dbContext, IStockVariantReader variants, IOptions<WarehouseOptions> options, TimeProvider timeProvider)
{
    public async Task EvaluateAsync(long variantRefId, CancellationToken cancellationToken)
    {
        var variant = await variants.FindByIdAsync(variantRefId, cancellationToken);
        if (variant is null)
        {
            return;
        }

        var available = await variants.GetOnHandAsync(variantRefId, cancellationToken);
        var everHeld = await variants.HasEverHeldStockAsync(variantRefId, cancellationToken);
        var threshold = await dbContext.Thresholds.AsNoTracking().Where(t => t.VariantRefId == variantRefId).Select(t => (int?)t.Quantity).FirstOrDefaultAsync(cancellationToken)
            ?? options.Value.DefaultLowStockThreshold;
        var open = await dbContext.Alerts.FirstOrDefaultAsync(a => a.VariantRefId == variantRefId && a.StatusId == AlertStatuses.Open, cancellationToken);

        var decision = AlertPolicy.Evaluate(available, threshold, everHeld, open?.LevelId);
        var now = timeProvider.GetUtcNow();
        switch (decision.Action)
        {
            case AlertAction.Open:
                dbContext.Alerts.Add(StockAlert.Open(variantRefId, variant.VariantId, variant.SkuCode, decision.Level, available, threshold, now));
                break;
            case AlertAction.ChangeLevel:
                open!.ChangeLevel(variant.VariantId, variant.SkuCode, decision.Level, available, threshold);
                break;
            case AlertAction.Close:
                open!.Close(now, available, threshold);
                break;
            default:
                open?.Observe(available, threshold);
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Re-evaluates a variant after a stock change. Sent by the outbox consumer for the module's own <c>StockLevelChangedEvent</c> (ALR-9).</summary>
public sealed record EvaluateStockAlertCommand(long VariantId) : IRequest, IWarehouseCommand;

public sealed class EvaluateStockAlertCommandHandler(IStockVariantReader variants, AlertEvaluator evaluator) : IRequestHandler<EvaluateStockAlertCommand>
{
    public async Task Handle(EvaluateStockAlertCommand request, CancellationToken cancellationToken)
    {
        var variant = await variants.FindByVariantIdAsync(request.VariantId, cancellationToken);
        if (variant is not null)
        {
            await evaluator.EvaluateAsync(variant.Id, cancellationToken);
        }
    }
}

/// <summary>Sets a variant's own low-stock threshold and re-evaluates its alert straight away (ALR-6).</summary>
public sealed record SetThresholdCommand(long VariantRefId, int Quantity, string ActingPerson) : IRequest<ThresholdRowDto>, IWarehouseCommand;

public sealed class SetThresholdCommandValidator : AbstractValidator<SetThresholdCommand>
{
    public SetThresholdCommandValidator()
    {
        RuleFor(c => c.VariantRefId).GreaterThan(0);
        RuleFor(c => c.Quantity).InclusiveBetween(0, LowStockThreshold.Max).WithMessage($"A low-stock threshold is a whole number from 0 to {LowStockThreshold.Max}.");
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class SetThresholdCommandHandler(IWarehouseDbContext dbContext, IStockVariantReader variants, AlertEvaluator evaluator)
    : IRequestHandler<SetThresholdCommand, ThresholdRowDto>
{
    public async Task<ThresholdRowDto> Handle(SetThresholdCommand request, CancellationToken cancellationToken)
    {
        var variant = await variants.FindByIdAsync(request.VariantRefId, cancellationToken)
            ?? throw new KeyNotFoundException($"Variant {request.VariantRefId} was not found.");

        var existing = await dbContext.Thresholds.FirstOrDefaultAsync(t => t.VariantRefId == request.VariantRefId, cancellationToken);
        if (existing is null)
        {
            dbContext.Thresholds.Add(new LowStockThreshold(request.VariantRefId, request.Quantity));
        }
        else
        {
            existing.Set(request.Quantity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await evaluator.EvaluateAsync(request.VariantRefId, cancellationToken);

        return new ThresholdRowDto(variant.Id, variant.SkuCode, variant.Gtin, await variants.GetOnHandAsync(variant.Id, cancellationToken), request.Quantity, true);
    }
}

/// <summary>Records who has seen an open alert (ALR-5). It stays open until stock recovers.</summary>
public sealed record AcknowledgeAlertCommand(long AlertId, string ActingPerson) : IRequest<AlertRowDto>, IWarehouseCommand;

public sealed class AcknowledgeAlertCommandValidator : AbstractValidator<AcknowledgeAlertCommand>
{
    public AcknowledgeAlertCommandValidator()
    {
        RuleFor(c => c.AlertId).GreaterThan(0);
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class AcknowledgeAlertCommandHandler(IWarehouseDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<AcknowledgeAlertCommand, AlertRowDto>
{
    public async Task<AlertRowDto> Handle(AcknowledgeAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await dbContext.Alerts.FirstOrDefaultAsync(a => a.Id == request.AlertId, cancellationToken)
            ?? throw new KeyNotFoundException($"Alert {request.AlertId} was not found.");
        alert.Acknowledge(request.ActingPerson.Trim(), timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        var variant = await dbContext.VariantRefs.AsNoTracking().FirstAsync(v => v.Id == alert.VariantRefId, cancellationToken);
        return new AlertRowDto(
            alert.Id, alert.VariantRefId, variant.SkuCode, variant.Gtin, alert.LevelId, AlertLevels.CodeOf(alert.LevelId), alert.LastQuantity, alert.Threshold,
            alert.OpenedAt, true, alert.AcknowledgedBy, alert.AcknowledgedAt);
    }
}
