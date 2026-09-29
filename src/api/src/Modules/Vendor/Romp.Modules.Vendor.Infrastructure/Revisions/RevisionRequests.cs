using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Revisions;

namespace Romp.Modules.Vendor.Infrastructure.Revisions;

/// <summary>SCRUM-93 task 29. POST body for proposing an amendment - the PO id comes from the route.</summary>
public sealed record CreateAmendmentRequest(
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
    IReadOnlyCollection<PoLineDto> Lines,
    IReadOnlyCollection<AmendmentFileAdd>? AddFiles = null,
    IReadOnlyCollection<long>? RetireFileIds = null)
{
    public CreateAmendmentCommand ToCommand(long poId) => new(
        poId, InitiatorId, ReasonId, ImpactNote, VendorMessage,
        UnitCost, ExpectedDeliveryDate, LatestAcceptableDate, OverTolerancePercent, UnderTolerancePercent,
        PaymentTermId, AdvancePercent, FabricResponsibilityId, Lines, AddFiles, RetireFileIds);
}

/// <summary>SCRUM-93 task 29. POST body for rejecting or withdrawing a revision - both are optional-note actions, the PO id and revision number come from the route.</summary>
public sealed record RevisionNoteRequest(string? Note);
