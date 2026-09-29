using Romp.Modules.Vendor.Domain;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Vendors;

internal static class VendorMapper
{
    public static VendorDto ToDto(this Domain.Vendors.Vendor vendor) => new(
        vendor.Id,
        vendor.Name,
        vendor.ContactName,
        vendor.ContactPhone,
        vendor.ContactEmail,
        vendor.CityId,
        vendor.PaymentTermId,
        vendor.OnTimePercent,
        vendor.OnQuantityPercent,
        vendor.DefectRatePercent,
        vendor.IsActive,
        vendor.Specialisations.Select(s => s.SpecialisationId).ToList());

    public static VendorSummaryDto ToSummaryDto(this Domain.Vendors.Vendor vendor) => new(vendor.Id, vendor.Name, vendor.CityId, vendor.IsActive);
}
