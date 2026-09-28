using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Vendor.Domain;

namespace Romp.Modules.Vendor.Application;

/// <summary>
/// SCRUM-93 task 22 (ADR 0007, FR-SC-03). Proposes a new revision from a PO's current (In-force)
/// terms plus the requested changes - AC-9 (SentToVendor: immediately In-force) / AC-10
/// (Acknowledged: Pending) is decided by the handler from the PO's own status, not by the caller.
/// </summary>
public sealed record CreateAmendmentCommand(
    long PoId,
    short InitiatorId,
    short ReasonId,
    string ImpactNote,
    string? VendorMessage,
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    DateOnly? LatestAcceptableDate,
    decimal? OverTolerancePercent,
    decimal? UnderTolerancePercent,
    short PaymentTermId,
    decimal AdvancePercent,
    short? FabricResponsibilityId,
    IReadOnlyCollection<PoLineDto> Lines) : IRequest<PoRevisionDto>, IVendorCommand;

public sealed class CreateAmendmentCommandValidator : AbstractValidator<CreateAmendmentCommand>
{
    public CreateAmendmentCommandValidator(PoCommercialTermsOptions termsOptions)
    {
        RuleFor(c => c.UnitCost).GreaterThan(0);
        RuleFor(c => c.ReasonId).GreaterThan((short)0).WithMessage("A reason is required.");
        RuleFor(c => c.ImpactNote).NotEmpty().WithMessage("An internal impact note is required."); // AC-17
        RuleFor(c => c.Lines).NotEmpty().WithMessage("At least one size/colour line is required.");
        RuleForEach(c => c.Lines).ChildRules(line => line.RuleFor(l => l.Qty).GreaterThan(0));

        // AC-5 (same rules as UpdatePurchaseOrderCommand - these terms are amendable here too, R6).
        RuleFor(c => c.LatestAcceptableDate)
            .Must((command, latestAcceptableDate) =>
                latestAcceptableDate is null || latestAcceptableDate >= command.ExpectedDeliveryDate)
            .WithMessage("Latest acceptable date must be on or after the expected delivery date.");
        RuleFor(c => c.OverTolerancePercent)
            .Must(pct => pct is null || (pct >= 0 && pct <= termsOptions.MaxTolerancePercent))
            .WithMessage($"Over-ship tolerance must be between 0 and {termsOptions.MaxTolerancePercent}%.");
        RuleFor(c => c.UnderTolerancePercent)
            .Must(pct => pct is null || (pct >= 0 && pct <= termsOptions.MaxTolerancePercent))
            .WithMessage($"Under-ship tolerance must be between 0 and {termsOptions.MaxTolerancePercent}%.");
    }
}

public sealed class CreateAmendmentCommandHandler(IVendorDbContext dbContext, IStyleQueries styleQueries)
    : IRequestHandler<CreateAmendmentCommand, PoRevisionDto>
{
    public async Task<PoRevisionDto> Handle(CreateAmendmentCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.Revisions)
            .FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        // AC-20: Draft/Cancelled can't be amended - use Update (Draft) or nothing (Cancelled) instead.
        // PoStatus is internal to Romp.Modules.Vendor.Domain (a different assembly) - literals with
        // comments match this codebase's existing convention for referencing them from outside.
        if (po.StatusId is 1 or 4 /* Draft or Cancelled */)
        {
            throw new DomainException(
                $"PO {po.PoNo} cannot be amended while in its current status (only Sent to Vendor or Acknowledged can be amended).");
        }

        // AC-14: at most one open Pending revision at a time. RevisionStatus is likewise internal.
        var openPending = po.Revisions.FirstOrDefault(r => r.StatusId == 1 /* Pending */);
        if (openPending is not null)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.PoId)] = [$"PO {po.PoNo} already has an open Pending revision (Rev {openPending.RevisionNumber}) - decide or withdraw it before proposing another."],
            });
        }

        // AC-15: line changes are validated against the style's size run/colourways, same rule as
        // Sprint 1's own PO creation (AC-8) - vendor and style themselves have no field on this
        // command at all, so they're structurally not amendable rather than checked here.
        var style = await styleQueries.FindActiveStyleAsync(po.StyleId, cancellationToken);
        var invalidLines = style is null
            ? request.Lines
            : request.Lines.Where(l => !style.SizeIds.Contains(l.SizeId) || !style.ColourIds.Contains(l.ColourId)).ToList();
        if (invalidLines.Count > 0)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Lines)] = [$"{invalidLines.Count} line(s) reference a size or colour not in this PO's style."],
            });
        }

        // AC-16: a true no-op (identical terms and lines to the PO's current position) is rejected.
        var requestedLines = request.Lines.Select(l => (l.SizeId, l.ColourId, l.Qty)).ToList();
        if (IsNoOp(po, request, requestedLines))
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.PoId)] = ["This amendment doesn't change anything from the PO's current terms or lines."],
            });
        }

        // AC-9 vs AC-10: SentToVendor goes immediately In-force; Acknowledged sits Pending.
        var goesImmediatelyInForce = po.StatusId == 2 /* SentToVendor */;

        var revision = po.CreateRevision(
            request.InitiatorId,
            request.ReasonId,
            request.ImpactNote,
            request.VendorMessage,
            request.UnitCost,
            request.ExpectedDeliveryDate,
            request.LatestAcceptableDate,
            request.OverTolerancePercent,
            request.UnderTolerancePercent,
            request.PaymentTermId,
            request.AdvancePercent,
            request.FabricResponsibilityId,
            requestedLines,
            goesImmediatelyInForce);

        await dbContext.SaveChangesAsync(cancellationToken);

        return revision.ToDto();
    }

    private static bool IsNoOp(PurchaseOrder po, CreateAmendmentCommand request, List<(short SizeId, short ColourId, int Qty)> requestedLines)
    {
        var termsUnchanged =
            po.UnitCost == request.UnitCost &&
            po.ExpectedDeliveryDate == request.ExpectedDeliveryDate &&
            po.LatestAcceptableDate == request.LatestAcceptableDate &&
            po.OverTolerancePercent == request.OverTolerancePercent &&
            po.UnderTolerancePercent == request.UnderTolerancePercent &&
            po.PaymentTermId == request.PaymentTermId &&
            po.AdvancePercent == request.AdvancePercent &&
            po.FabricResponsibilityId == request.FabricResponsibilityId;

        if (!termsUnchanged)
        {
            return false;
        }

        var currentLines = po.Lines.Select(l => (l.SizeId, l.ColourId, l.Qty)).ToHashSet();
        return currentLines.SetEquals(requestedLines);
    }
}
