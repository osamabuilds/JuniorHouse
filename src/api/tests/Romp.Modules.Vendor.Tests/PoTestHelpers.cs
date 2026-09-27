using MediatR;
using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Tests;

internal static class PoTestHelpers
{
    public static async Task<VendorDto> CreateActiveVendorAsync(ISender sender, short paymentTermId = FakePaymentTermQueries.TermId) =>
        await sender.Send(new CreateVendorCommand(
            "Test Vendor", "Contact", "0300-0000000", null, CityId: 1, PaymentTermId: paymentTermId, SpecialisationIds: [1]));

    public static async Task<PoDto> CreateDraftPoAsync(ISender sender, long? vendorId = null)
    {
        var resolvedVendorId = vendorId ?? (await CreateActiveVendorAsync(sender)).Id;

        return await sender.Send(new CreatePurchaseOrderCommand(
            resolvedVendorId,
            FakeStyleQueries.ActiveStyle.Id,
            UnitCost: 500m,
            ExpectedDeliveryDate: new DateOnly(2026, 12, 1),
            Lines: [new PoLineDto(1, 10, 100), new PoLineDto(2, 20, 50)]));
    }
}
