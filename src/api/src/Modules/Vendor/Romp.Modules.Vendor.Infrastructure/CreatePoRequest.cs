using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>PUT request body for editing a Draft PO - the id comes from the route.</summary>
public sealed record UpdatePoRequest(
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    short PaymentTermId,
    decimal AdvancePercent,
    IReadOnlyCollection<PoLineDto> Lines)
{
    public UpdatePurchaseOrderCommand ToCommand(long id) => new(id, UnitCost, ExpectedDeliveryDate, PaymentTermId, AdvancePercent, Lines);
}

public sealed record CancelPoRequest(short CancelReasonId);

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
