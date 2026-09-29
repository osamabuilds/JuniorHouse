using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Revisions;
using Romp.Modules.Vendor.Domain;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.VendorResponses;

/// <summary>
/// SCRUM-93 task 31 (AC-30, AC-31, ADR 0007). The vendor-side counterpart to
/// <see cref="CreateAmendmentCommand"/>'s Acknowledged branch: on an Acknowledged PO, the vendor
/// proposes new terms/lines, which sit as a Pending revision for the buyer to accept/reject (task
/// 24) - the PO itself stays Acknowledged either way (AC-28). Unlike <see cref="RecordVendorResponseCommand"/>,
/// there is no revision-number staleness check to make here - proposing a new Pending revision
/// doesn't reference (or risk conflicting with) any specific prior revision number.
/// </summary>
public sealed record RecordVendorAmendmentRequestCommand(
    long PoId,
    short ChannelId,
    string ResponderName,
    DateTimeOffset? ResponseDte,
    CounterProposal Request,
    IReadOnlyCollection<EvidenceFileAdd>? Evidence = null) : IRequest<PoRevisionDto>, IVendorCommand;

public sealed class RecordVendorAmendmentRequestCommandValidator : AbstractValidator<RecordVendorAmendmentRequestCommand>
{
    public RecordVendorAmendmentRequestCommandValidator(PoCommercialTermsOptions termsOptions, PoFileStorageOptions fileOptions)
    {
        RuleFor(c => c.ChannelId).GreaterThan((short)0).WithMessage("A channel is required."); // AC-31
        RuleForEach(c => c.Evidence).SetValidator(new EvidenceFileAddValidator(fileOptions));
        RuleFor(c => c.ResponderName).NotEmpty();
        RuleFor(c => c.ResponseDte)
            .Must(dte => dte is null || dte <= DateTimeOffset.UtcNow)
            .WithMessage("Response time cannot be in the future.");

        RuleFor(c => c.Request.UnitCost).GreaterThan(0);
        RuleFor(c => c.Request.ReasonId).GreaterThan((short)0).WithMessage("A reason is required.");
        RuleFor(c => c.Request.ImpactNote).NotEmpty().WithMessage("An internal impact note is required."); // AC-17
        RuleFor(c => c.Request.Lines).NotEmpty().WithMessage("At least one size/colour line is required.");
        RuleForEach(c => c.Request.Lines).ChildRules(line => line.RuleFor(l => l.Qty).GreaterThan(0));
        RuleFor(c => c.Request.LatestAcceptableDate)
            .Must((command, latestAcceptableDate) =>
                latestAcceptableDate is null || latestAcceptableDate >= command.Request.ExpectedDeliveryDate)
            .WithMessage("Latest acceptable date must be on or after the expected delivery date.");
        RuleFor(c => c.Request.OverTolerancePercent)
            .Must(pct => pct is null || (pct >= 0 && pct <= termsOptions.MaxTolerancePercent))
            .WithMessage($"Over-ship tolerance must be between 0 and {termsOptions.MaxTolerancePercent}%.");
        RuleFor(c => c.Request.UnderTolerancePercent)
            .Must(pct => pct is null || (pct >= 0 && pct <= termsOptions.MaxTolerancePercent))
            .WithMessage($"Under-ship tolerance must be between 0 and {termsOptions.MaxTolerancePercent}%.");
    }
}

public sealed class RecordVendorAmendmentRequestCommandHandler(IVendorDbContext dbContext, IStyleQueries styleQueries, IFileStorage fileStorage)
    : IRequestHandler<RecordVendorAmendmentRequestCommand, PoRevisionDto>
{
    public async Task<PoRevisionDto> Handle(RecordVendorAmendmentRequestCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.Revisions).ThenInclude(r => r.Lines)
            .FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        // AC-30: only an Acknowledged PO can carry a vendor amendment request.
        if (po.StatusId != 3 /* PoStatus.Acknowledged */)
        {
            throw new DomainException(
                $"PO {po.PoNo} must be Acknowledged to record a vendor amendment request (current status does not allow it).");
        }

        // AC-14: at most one open Pending revision at a time.
        var openPending = po.Revisions.FirstOrDefault(r => r.StatusId == 1 /* RevisionStatus.Pending */);
        if (openPending is not null)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.PoId)] = [$"PO {po.PoNo} already has an open Pending revision (Rev {openPending.RevisionNumber}) - decide or withdraw it before proposing another."],
            });
        }

        var req = request.Request;

        // AC-15: line changes are validated against the style's size run/colourways.
        var style = await styleQueries.FindActiveStyleAsync(po.StyleId, cancellationToken);
        var invalidLines = style is null
            ? req.Lines
            : req.Lines.Where(l => !style.SizeIds.Contains(l.SizeId) || !style.ColourIds.Contains(l.ColourId)).ToList();
        if (invalidLines.Count > 0)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.Request.Lines)] = [$"{invalidLines.Count} line(s) reference a size or colour not in this PO's style."],
            });
        }

        var requestedLines = req.Lines.Select(l => (l.SizeId, l.ColourId, l.Qty)).ToList();

        // AC-16: a true no-op (identical terms and lines to the PO's current position) is rejected.
        if (IsNoOp(po, req, requestedLines))
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.PoId)] = ["This amendment request doesn't change anything from the PO's current terms or lines."],
            });
        }

        var revision = po.CreateRevision(
            2 /* AmendmentInitiator.Vendor */,
            req.ReasonId,
            req.ImpactNote,
            req.VendorMessage,
            req.UnitCost,
            req.ExpectedDeliveryDate,
            req.LatestAcceptableDate,
            req.OverTolerancePercent,
            req.UnderTolerancePercent,
            req.PaymentTermId,
            req.AdvancePercent,
            req.FabricResponsibilityId,
            requestedLines,
            goesImmediatelyInForce: false); // AC-30: Acknowledged always sits Pending, never immediate.

        // The new revision's id isn't real until saved - realize it before linking the
        // communication to it (PurchaseOrder.RecordVendorCommunication's own requirement).
        await dbContext.SaveChangesAsync(cancellationToken);

        var responseDte = request.ResponseDte ?? DateTimeOffset.UtcNow;
        var communication = po.RecordVendorCommunication(4 /* PoVndrCommType.AmendmentRequest */, request.ChannelId, request.ResponderName, responseDte, revision.Id);
        await dbContext.SaveChangesAsync(cancellationToken); // the communication needs its real id before evidence can point at it
        await VendorEvidence.AddAsync(dbContext, fileStorage, po.Id, communication, request.Evidence, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return revision.ToDto();
    }

    private static bool IsNoOp(PurchaseOrder po, CounterProposal request, List<(short SizeId, short ColourId, int Qty)> requestedLines)
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
