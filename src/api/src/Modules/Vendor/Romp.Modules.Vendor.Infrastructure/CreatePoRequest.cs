using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>PUT request body for editing a Draft PO - the id comes from the route.</summary>
public sealed record UpdatePoRequest(
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    short PaymentTermId,
    decimal AdvancePercent,
    IReadOnlyCollection<PoLineDto> Lines,
    DateOnly? LatestAcceptableDate = null,
    decimal? OverTolerancePercent = null,
    decimal? UnderTolerancePercent = null,
    short? FabricResponsibilityId = null)
{
    public UpdatePurchaseOrderCommand ToCommand(long id) => new(
        id, UnitCost, ExpectedDeliveryDate, PaymentTermId, AdvancePercent, Lines,
        LatestAcceptableDate, OverTolerancePercent, UnderTolerancePercent, FabricResponsibilityId);
}

public sealed record CancelPoRequest(short CancelReasonId);

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
    IReadOnlyCollection<PoLineDto> Lines)
{
    public CreateAmendmentCommand ToCommand(long poId) => new(
        poId, InitiatorId, ReasonId, ImpactNote, VendorMessage,
        UnitCost, ExpectedDeliveryDate, LatestAcceptableDate, OverTolerancePercent, UnderTolerancePercent,
        PaymentTermId, AdvancePercent, FabricResponsibilityId, Lines);
}

/// <summary>SCRUM-93 task 29. POST body for rejecting or withdrawing a revision - both are optional-note actions, the PO id and revision number come from the route.</summary>
public sealed record RevisionNoteRequest(string? Note);

/// <summary>PUT request body for editing a vendor - the id comes from the route.</summary>
public sealed record UpdateVendorRequest(
    string ContactName,
    string ContactPhone,
    string? ContactEmail,
    short CityId,
    short PaymentTermId,
    IReadOnlyCollection<short> SpecialisationIds,
    bool IsActive)
{
    public UpdateVendorCommand ToCommand(long id) =>
        new(id, ContactName, ContactPhone, ContactEmail, CityId, PaymentTermId, SpecialisationIds, IsActive);
}
