using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Application.Bins;
using Romp.Modules.Warehouse.Application.StockLedger;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;
using Romp.Modules.Warehouse.Domain.Putaway;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Application.Adjustments;

public sealed record AdjustmentDto(
    long Id,
    long VariantRefId,
    string SkuCode,
    string Gtin,
    long BinId,
    string BinCode,
    short GradeId,
    string GradeCode,
    int QuantityChange,
    short ReasonId,
    string ReasonCode,
    string? Note,
    bool Reconfirmed,
    int BalanceBefore,
    int BalanceAfter,
    string ActorName,
    DateTimeOffset At);

/// <summary>
/// One adjustment as the poster takes it. Either <see cref="VariantRefId"/> or <see cref="Gtin"/> names the variant. The idempotency key
/// makes a resend apply once (ADJ-8). <see cref="CountLineId"/> is set when a cycle count raises the adjustment.
/// </summary>
public sealed record AdjustmentRequest(
    long? VariantRefId, string? Gtin, string BinCode, short GradeId, int QuantityChange, short ReasonId, string? Note, bool Reconfirmed,
    Guid IdempotencyKey, long? CountLineId, string ActingPerson);

/// <summary>
/// Adjustments' public surface for cycle counts (plan): the same rules and the same transaction as a manual adjustment, without HTTP.
/// Runs inside the caller's transaction.
/// </summary>
public interface IAdjustmentPoster
{
    Task<AdjustmentDto> PostAsync(AdjustmentRequest request, CancellationToken cancellationToken);
}

public sealed class AdjustmentPoster(
    IWarehouseDbContext dbContext,
    IBinReader bins,
    IStockVariantReader variants,
    IStockPosting posting,
    IOptions<WarehouseOptions> options) : IAdjustmentPoster
{
    public async Task<AdjustmentDto> PostAsync(AdjustmentRequest request, CancellationToken cancellationToken)
    {
        var repeat = await dbContext.Adjustments.AsNoTracking().FirstOrDefaultAsync(a => a.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (repeat is not null)
        {
            return await ToDtoAsync(repeat, cancellationToken);
        }

        if (!Grades.IsKnown(request.GradeId))
        {
            throw new DomainException($"Unknown stock grade {request.GradeId}.");
        }

        AdjustmentPolicy.Check(request.ReasonId, request.QuantityChange, options.Value.LargeAdjustmentUnits, request.Reconfirmed);

        var variant = request.VariantRefId is { } id
            ? await variants.FindByIdAsync(id, cancellationToken)
            : await variants.FindByGtinAsync(request.Gtin ?? string.Empty, cancellationToken);
        if (variant is null)
        {
            throw new DomainException("No variant matches that barcode or id.");
        }

        // ADJ-9: stock still on the shelf under a retired barcode can be taken off the books, but nothing new is added under it.
        if (variant.GtinRetired && request.QuantityChange > 0)
        {
            throw new DomainException("This barcode has been retired, so units cannot be added under it.");
        }

        var bin = await bins.FindByCodeAsync(request.BinCode, cancellationToken)
            ?? throw new DomainException($"Bin {request.BinCode.Trim().ToUpperInvariant()} does not exist.");
        if (!bin.IsActive)
        {
            throw new DomainException($"Bin {bin.BinCode} is inactive.");
        }

        var actor = request.ActingPerson.Trim();
        var adjustment = new StockAdjustment(
            variant.Id, request.GradeId, bin.Id, request.QuantityChange, request.ReasonId, request.Note?.Trim(), request.Reconfirmed,
            request.IdempotencyKey, request.CountLineId, actor);
        dbContext.Adjustments.Add(adjustment);
        await dbContext.SaveChangesAsync(cancellationToken); // gives the adjustment its id, which the ledger row points back at

        var posted = await posting.PostAsync(
            [new StockPost(variant.Id, request.GradeId, bin.Id, request.QuantityChange, MovementKinds.Adjustment, adjustment.Id)],
            actor,
            cancellationToken);

        return new AdjustmentDto(
            adjustment.Id, variant.Id, variant.SkuCode, variant.Gtin, bin.Id, bin.BinCode, request.GradeId, Grades.CodeOf(request.GradeId),
            adjustment.QuantityChange, adjustment.ReasonId, AdjustmentReasons.CodeOf(adjustment.ReasonId), adjustment.Note, adjustment.Reconfirmed,
            posted[0].BalanceBefore, posted[0].BalanceAfter, actor, adjustment.InsrDte);
    }

    private async Task<AdjustmentDto> ToDtoAsync(StockAdjustment a, CancellationToken cancellationToken)
    {
        var variant = await variants.FindByIdAsync(a.VariantRefId, cancellationToken)
            ?? throw new InvalidOperationException("The adjustment's variant is missing.");
        var binCode = await dbContext.Bins.AsNoTracking().Where(b => b.Id == a.BinId).Select(b => b.BinCode).FirstAsync(cancellationToken);
        var movement = await dbContext.Movements.AsNoTracking()
            .Where(m => m.KindId == MovementKinds.Adjustment && m.SourceId == a.Id)
            .Select(m => new { m.BalanceBefore, m.BalanceAfter })
            .FirstAsync(cancellationToken);
        return new AdjustmentDto(
            a.Id, a.VariantRefId, variant.SkuCode, variant.Gtin, a.BinId, binCode, a.GradeId, Grades.CodeOf(a.GradeId), a.QuantityChange,
            a.ReasonId, AdjustmentReasons.CodeOf(a.ReasonId), a.Note, a.Reconfirmed, movement.BalanceBefore, movement.BalanceAfter, a.ActorName, a.InsrDte);
    }
}

/// <summary>Posts one manual adjustment (ADJ-0..9, FR-54): a signed change to one bin, with a reason, recorded and on the ledger in one transaction.</summary>
public sealed record PostAdjustmentCommand(
    long? VariantRefId, string? Gtin, string BinCode, short GradeId, int QuantityChange, short ReasonId, string? Note, bool Reconfirmed,
    Guid IdempotencyKey, string ActingPerson) : IRequest<AdjustmentDto>, IWarehouseCommand;

public sealed class PostAdjustmentCommandValidator : AbstractValidator<PostAdjustmentCommand>
{
    public PostAdjustmentCommandValidator()
    {
        RuleFor(c => c).Must(c => c.VariantRefId is > 0 || Gtin13.IsValid(c.Gtin))
            .WithName("Gtin").WithMessage("Scan a valid GTIN-13 barcode or choose a variant.");
        RuleFor(c => c.BinCode).NotEmpty().MaximumLength(12);
        RuleFor(c => c.GradeId).Must(Grades.IsKnown).WithMessage("Choose a stock grade.");
        RuleFor(c => c.ReasonId).Must(AdjustmentReasons.IsKnown).WithMessage("Choose a reason: damage, miscount, theft or found stock.");
        RuleFor(c => c.QuantityChange).NotEqual(0).WithMessage("The change cannot be zero.")
            .InclusiveBetween(-AdjustmentPolicy.MaxChange, AdjustmentPolicy.MaxChange);
        RuleFor(c => c.Note).OptionalNote();
        RuleFor(c => c.IdempotencyKey).NotEmpty();
        RuleFor(c => c.ActingPerson).ActingPerson();
    }
}

public sealed class PostAdjustmentCommandHandler(IAdjustmentPoster poster) : IRequestHandler<PostAdjustmentCommand, AdjustmentDto>
{
    public Task<AdjustmentDto> Handle(PostAdjustmentCommand request, CancellationToken cancellationToken) =>
        poster.PostAsync(
            new AdjustmentRequest(
                request.VariantRefId, request.Gtin, request.BinCode, request.GradeId, request.QuantityChange, request.ReasonId, request.Note,
                request.Reconfirmed, request.IdempotencyKey, null, request.ActingPerson),
            cancellationToken);
}
