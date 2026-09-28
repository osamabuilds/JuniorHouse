using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Domain;

namespace Romp.Modules.Vendor.Application;

/// <summary>SCRUM-93 task 30. The counter-proposal's terms/lines when <see cref="RecordVendorResponseCommand.OutcomeTypeId"/> is Countered - reuses the same shape as <see cref="CreateAmendmentCommand"/> since it's the same underlying revision-creation logic (task 22), just initiated by the vendor.</summary>
public sealed record CounterProposal(
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
    IReadOnlyCollection<PoLineDto> Lines);

/// <summary>
/// SCRUM-93 task 30 (AC-25..AC-31). <see cref="OutcomeTypeId"/> is REF.PO_VNDR_COMM_TYP_LKP's seeded
/// id: Confirmed=1, Countered=2, Declined=3. <see cref="RevisionNumber"/> must name the PO's current
/// In-force revision (AC-26, staleness check) for every outcome, not only Confirmed.
/// </summary>
public sealed record RecordVendorResponseCommand(
    long PoId,
    short OutcomeTypeId,
    short RevisionNumber,
    short ChannelId,
    string ResponderName,
    DateTimeOffset? ResponseDte,
    CounterProposal? Counter,
    IReadOnlyCollection<EvidenceFileAdd>? Evidence = null) : IRequest<RecordVendorResponseResult>, IVendorCommand;

/// <summary>Declined carries no state change of its own - <see cref="SuggestedCancelReasonId"/> is the signal the API layer uses to pre-fill Cancel with VendorDeclined (AC-29).</summary>
public sealed record RecordVendorResponseResult(short OutcomeTypeId, PoDto Po, PoRevisionDto? Revision, short? SuggestedCancelReasonId);

public sealed class RecordVendorResponseCommandValidator : AbstractValidator<RecordVendorResponseCommand>
{
    public RecordVendorResponseCommandValidator(PoFileStorageOptions fileOptions)
    {
        RuleFor(c => c.ChannelId).GreaterThan((short)0).WithMessage("A channel is required."); // AC-31
        RuleForEach(c => c.Evidence).SetValidator(new EvidenceFileAddValidator(fileOptions));
        RuleFor(c => c.ResponderName).NotEmpty();
        RuleFor(c => c.ResponseDte)
            .Must(dte => dte is null || dte <= DateTimeOffset.UtcNow)
            .WithMessage("Response time cannot be in the future.");
        RuleFor(c => c.OutcomeTypeId).InclusiveBetween((short)1, (short)3).WithMessage("Outcome must be Confirmed, Countered, or Declined.");
        RuleFor(c => c.Counter).NotNull().When(c => c.OutcomeTypeId == 2).WithMessage("Counter terms are required for a Countered response.");
    }
}

public sealed class RecordVendorResponseCommandHandler(IVendorDbContext dbContext, IFileStorage fileStorage)
    : IRequestHandler<RecordVendorResponseCommand, RecordVendorResponseResult>
{
    public async Task<RecordVendorResponseResult> Handle(RecordVendorResponseCommand request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .Include(p => p.Revisions).ThenInclude(r => r.Lines)
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        var responseDte = request.ResponseDte ?? DateTimeOffset.UtcNow;

        // AC-26: the revision number named must be the PO's current In-force one, for every outcome.
        var inForceRevision = po.Revisions.FirstOrDefault(r => r.StatusId == 2 /* RevisionStatus.InForce */);
        var currentRevisionNumber = inForceRevision?.RevisionNumber ?? (short)0;
        if (request.RevisionNumber != currentRevisionNumber)
        {
            throw new Romp.BuildingBlocks.Application.ValidationException(new Dictionary<string, string[]>
            {
                [nameof(request.RevisionNumber)] = [$"PO {po.PoNo}'s latest revision is Rev {currentRevisionNumber}."],
            });
        }

        PoRevisionDto? revisionDto = null;
        short? suggestedCancelReasonId = null;

        PoVendorCommunication communication = null!; // assigned in every case below (outcome is validated as 1-3)
        switch (request.OutcomeTypeId)
        {
            case 1: // Confirmed
                po.Acknowledge();
                communication = po.RecordVendorCommunication(request.OutcomeTypeId, request.ChannelId, request.ResponderName, responseDte, inForceRevision?.Id);
                break;

            case 2: // Countered
                var counter = request.Counter!;
                var revision = po.CreateRevision(
                    2 /* AmendmentInitiator.Vendor */,
                    counter.ReasonId,
                    counter.ImpactNote,
                    counter.VendorMessage,
                    counter.UnitCost,
                    counter.ExpectedDeliveryDate,
                    counter.LatestAcceptableDate,
                    counter.OverTolerancePercent,
                    counter.UnderTolerancePercent,
                    counter.PaymentTermId,
                    counter.AdvancePercent,
                    counter.FabricResponsibilityId,
                    counter.Lines.Select(l => (l.SizeId, l.ColourId, l.Qty)).ToList(),
                    goesImmediatelyInForce: false);

                // The new revision's id isn't real until saved - realize it before linking the
                // communication to it (PurchaseOrder.RecordVendorCommunication's own requirement).
                await dbContext.SaveChangesAsync(cancellationToken);

                communication = po.RecordVendorCommunication(request.OutcomeTypeId, request.ChannelId, request.ResponderName, responseDte, revision.Id);
                revisionDto = revision.ToDto();
                break;

            case 3: // Declined
                communication = po.RecordVendorCommunication(request.OutcomeTypeId, request.ChannelId, request.ResponderName, responseDte, inForceRevision?.Id);
                suggestedCancelReasonId = 1; // PO_CNCL_RSN_LKP.VendorDeclined
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.Evidence is { Count: > 0 })
        {
            // The communication has its real id now (saved above); evidence files point at it.
            await VendorEvidence.AddAsync(dbContext, fileStorage, po.Id, communication, request.Evidence, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new RecordVendorResponseResult(request.OutcomeTypeId, po.ToDto(), revisionDto, suggestedCancelReasonId);
    }
}
