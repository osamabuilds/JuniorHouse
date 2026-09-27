using Romp.Modules.Vendor.Domain;

namespace Romp.Modules.Vendor.Application;

internal static class VendorMapper
{
    public static VendorDto ToDto(this Domain.Vendor vendor) => new(
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

    public static VendorSummaryDto ToSummaryDto(this Domain.Vendor vendor) => new(vendor.Id, vendor.Name, vendor.CityId, vendor.IsActive);
}
