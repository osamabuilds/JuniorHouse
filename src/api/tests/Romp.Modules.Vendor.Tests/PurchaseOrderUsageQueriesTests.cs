using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Domain;
using Romp.Modules.Vendor.Infrastructure;

namespace Romp.Modules.Vendor.Tests;

public sealed class PurchaseOrderUsageQueriesTests
{
    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task GetActiveSizeColourUsage_ReturnsSizesAndColoursInNonCancelledPos()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        const long styleId = 700;

        await using (var seedContext = new VendorDbContext(options))
        {
            var active = new PurchaseOrder(
                "PO-2026-00001", vendorId: 1, styleId, unitCost: 500m,
                expectedDeliveryDate: new DateOnly(2026, 12, 1),
                paymentTermId: 1, advancePercent: 0m,
                lines: [(SizeId: 1, ColourId: 10, Qty: 5)]);

            var cancelled = new PurchaseOrder(
                "PO-2026-00002", vendorId: 1, styleId, unitCost: 500m,
                expectedDeliveryDate: new DateOnly(2026, 12, 1),
                paymentTermId: 1, advancePercent: 0m,
                lines: [(SizeId: 2, ColourId: 20, Qty: 5)]);
            cancelled.Cancel(cancelReasonId: 1);

            var otherStyle = new PurchaseOrder(
                "PO-2026-00003", vendorId: 1, styleId + 1, unitCost: 500m,
                expectedDeliveryDate: new DateOnly(2026, 12, 1),
                paymentTermId: 1, advancePercent: 0m,
                lines: [(SizeId: 3, ColourId: 30, Qty: 5)]);

            seedContext.PurchaseOrders.AddRange(active, cancelled, otherStyle);
            await seedContext.SaveChangesAsync();
        }

        await using var context = new VendorDbContext(options);
        var usage = await new PurchaseOrderUsageQueries(context)
            .GetActiveSizeColourUsageAsync(styleId, CancellationToken.None);

        // Only the active PO's line for this style survives - the cancelled PO and the other
        // style's PO are both excluded.
        var row = Assert.Single(usage);
        Assert.Equal((short)1, row.SizeId);
        Assert.Equal((short)10, row.ColourId);
        Assert.Equal("PO-2026-00001", row.PoNo);
    }
}
