using Romp.Modules.Vendor.Application.Vendors;

namespace Romp.Modules.Vendor.Infrastructure.Vendors;

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
