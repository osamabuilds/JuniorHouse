using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.PurchaseOrders;

/// <summary>
/// AC-9: Draft only - quantities, cost, date, payment term, advance % all editable in place. AC-13:
/// rejected past Draft. The 4 trailing fields are SCRUM-93 task 16-18's new commercial terms (AC-5,
/// AC-8) - optional and defaulted to null so existing positional call sites (Sprint 1) keep compiling.
/// </summary>
public sealed record UpdatePurchaseOrderCommand(
    long Id,
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    short PaymentTermId,
    decimal AdvancePercent,
    IReadOnlyCollection<PoLineDto> Lines,
    DateOnly? LatestAcceptableDate = null,
    decimal? OverTolerancePercent = null,
    decimal? UnderTolerancePercent = null,
    short? FabricResponsibilityId = null) : IRequest<PoDto>, IVendorCommand;

public sealed class UpdatePurchaseOrderCommandValidator : AbstractValidator<UpdatePurchaseOrderCommand>
{
    public UpdatePurchaseOrderCommandValidator(PoCommercialTermsOptions termsOptions)
    {
        RuleFor(c => c.UnitCost).GreaterThan(0);
        RuleFor(c => c.Lines).NotEmpty().WithMessage("At least one size/colour line is required.");
        RuleForEach(c => c.Lines).ChildRules(line => line.RuleFor(l => l.Qty).GreaterThan(0));

        // AC-5: when set, the latest acceptable date must be on or after the expected date.
        RuleFor(c => c.LatestAcceptableDate)
            .Must((command, latestAcceptableDate) =>
                latestAcceptableDate is null || latestAcceptableDate >= command.ExpectedDeliveryDate)
            .WithMessage("Latest acceptable date must be on or after the expected delivery date.");

        // AC-5: when set, tolerances must be within 0 and the configured maximum.
        RuleFor(c => c.OverTolerancePercent)
            .Must(pct => pct is null || (pct >= 0 && pct <= termsOptions.MaxTolerancePercent))
            .WithMessage($"Over-ship tolerance must be between 0 and {termsOptions.MaxTolerancePercent}%.");
        RuleFor(c => c.UnderTolerancePercent)
            .Must(pct => pct is null || (pct >= 0 && pct <= termsOptions.MaxTolerancePercent))
            .WithMessage($"Under-ship tolerance must be between 0 and {termsOptions.MaxTolerancePercent}%.");
    }
}

public sealed class UpdatePurchaseOrderCommandHandler(IVendorDbContext dbContext)
    : IRequestHandler<UpdatePurchaseOrderCommand, PoDto>
{
    public async Task<PoDto> Handle(UpdatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.Id} was not found.");

        po.UpdateDraftDetails(
            request.UnitCost,
            request.ExpectedDeliveryDate,
            request.PaymentTermId,
            request.AdvancePercent,
            request.LatestAcceptableDate,
            request.OverTolerancePercent,
            request.UnderTolerancePercent,
            request.FabricResponsibilityId,
            request.Lines.Select(l => (l.SizeId, l.ColourId, l.Qty)));

        // Saved here (not left to VendorTransactionBehavior) so the response DTO reflects
        // UPDT_DTE/BY and the new lines' INSR_DTE/BY as stamped by AuditSaveChangesInterceptor,
        // not the pre-save in-memory values.
        await dbContext.SaveChangesAsync(cancellationToken);

        return po.ToDto();
    }
}
