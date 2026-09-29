using MediatR;
using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Application.Vendors;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Tests.Support;

internal static class PoTestHelpers
{
    public static async Task<VendorDto> CreateActiveVendorAsync(ISender sender, short paymentTermId = FakePaymentTermQueries.TermId) =>
        await sender.Send(new CreateVendorCommand(
            "Test Vendor", "Contact", "0300-0000000", null, CityId: 1, PaymentTermId: paymentTermId, SpecialisationIds: [1]));

    /// <summary>
    /// Creates a Draft PO with valid commercial terms already set (SCRUM-93 task 18, AC-6: Send now
    /// rejects a PO with no latest acceptable date/fabric responsibility) - Sprint 1 tests that use
    /// this helper and then Send don't know about these new fields, so this fills them in rather
    /// than every caller having to. <see cref="SendPurchaseOrderCommandHandlerTests"/>'s negative
    /// test builds its own PO without this step, to exercise the missing-terms rejection itself.
    /// </summary>
    public static async Task<PoDto> CreateDraftPoAsync(ISender sender, long? vendorId = null)
    {
        var resolvedVendorId = vendorId ?? (await CreateActiveVendorAsync(sender)).Id;

        var created = await sender.Send(new CreatePurchaseOrderCommand(
            resolvedVendorId,
            FakeStyleQueries.ActiveStyle.Id,
            UnitCost: 500m,
            ExpectedDeliveryDate: new DateOnly(2026, 12, 1),
            Lines: [new PoLineDto(1, 10, 100), new PoLineDto(2, 20, 50)]));

        return await sender.Send(new UpdatePurchaseOrderCommand(
            created.Id,
            created.UnitCost,
            created.ExpectedDeliveryDate,
            created.PaymentTermId,
            created.AdvancePercent,
            created.Lines,
            LatestAcceptableDate: created.ExpectedDeliveryDate.AddDays(14),
            FabricResponsibilityId: 1));
    }
}
