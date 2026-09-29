using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure.Persistence;

namespace Romp.Api.Seeding;

/// <summary>
/// SCRUM-175: a handful of sample styles and vendors for the "Try Sprint 1" walkthrough (README),
/// plus (SCRUM-93) one PO with a pending amendment for "Try Sprint 2".
/// Optional and idempotent - only inserts anything the first time (checks for existing rows), so
/// re-running `docker compose up` never duplicates the demo data. Development only, same as the
/// startup migrations it runs alongside (Program.cs).
/// </summary>
internal static class DemoDataSeeder
{
    public static async Task SeedAsync(CatalogDbContext catalogContext, VendorDbContext vendorContext, CancellationToken cancellationToken)
    {
        if (!await catalogContext.Styles.AnyAsync(cancellationToken))
        {
            var dress = new Style(
                "STY-DRESS-001", "Floral Summer Dress", "Summer 2026",
                categoryId: 3, genderId: 2, ageBracketId: 3, fabricId: 1,
                targetUnitCost: 450m, targetRetailPrice: 1200m);
            dress.SetColourSizeAndTargets(
                colourIds: [1, 2],
                sizeIds: [4, 5],
                targetLines: [(4, 1, 50), (4, 2, 40), (5, 1, 30), (5, 2, 25)]);

            var tee = new Style(
                "STY-TEE-001", "Basic Cotton Tee", "Core",
                categoryId: 1, genderId: 3, ageBracketId: 4, fabricId: 1,
                targetUnitCost: 200m, targetRetailPrice: 600m);
            tee.SetColourSizeAndTargets(
                colourIds: [1, 3],
                sizeIds: [7, 8],
                targetLines: [(7, 1, 100), (7, 3, 80), (8, 1, 90)]);

            catalogContext.Styles.AddRange(dress, tee);
            await catalogContext.SaveChangesAsync(cancellationToken);
        }

        if (!await vendorContext.Vendors.AnyAsync(cancellationToken))
        {
            var sialkotVendor = new Romp.Modules.Vendor.Domain.Vendors.Vendor(
                "Sialkot Garments Co.", "Ali Raza", "+92-300-1234567", "ali@sialkotgarments.pk",
                cityId: 7, paymentTermId: 2);
            sialkotVendor.SetSpecialisations([1, 3]);

            var lahoreVendor = new Romp.Modules.Vendor.Domain.Vendors.Vendor(
                "Lahore Knitwear Ltd.", "Bilal Khan", "+92-300-7654321", "bilal@lahoreknitwear.pk",
                cityId: 2, paymentTermId: 3);
            lahoreVendor.SetSpecialisations([1]);

            vendorContext.Vendors.AddRange(sialkotVendor, lahoreVendor);
            await vendorContext.SaveChangesAsync(cancellationToken);
        }

        await SeedSampleAmendmentAsync(catalogContext, vendorContext, cancellationToken);
    }

    /// <summary>
    /// SCRUM-93 task 56: one Acknowledged PO with a vendor's counter-proposal waiting as a Pending
    /// revision, so the "Try Sprint 2" walkthrough has an amendment to accept or reject without
    /// first raising and walking a PO. Each step is saved on its own so every event carries the
    /// PO's real id.
    /// </summary>
    private static async Task SeedSampleAmendmentAsync(CatalogDbContext catalogContext, VendorDbContext vendorContext, CancellationToken cancellationToken)
    {
        const string demoPoNo = "PO-2026-90001";
        if (await vendorContext.PurchaseOrders.AnyAsync(po => po.PoNo == demoPoNo, cancellationToken))
        {
            return;
        }

        var style = await catalogContext.Styles.FirstOrDefaultAsync(s => s.Code == "STY-DRESS-001", cancellationToken);
        var vendor = await vendorContext.Vendors.OrderBy(v => v.Id).FirstOrDefaultAsync(cancellationToken);
        if (style is null || vendor is null)
        {
            return;
        }

        (short SizeId, short ColourId, int Qty)[] lines = [(4, 1, 50), (4, 2, 40), (5, 1, 30), (5, 2, 25)];
        var po = new Romp.Modules.Vendor.Domain.PurchaseOrders.PurchaseOrder(
            demoPoNo, vendor.Id, style.Id, unitCost: 450m, expectedDeliveryDate: new DateOnly(2026, 12, 1),
            paymentTermId: 2, advancePercent: 40m, lines);
        vendorContext.PurchaseOrders.Add(po);
        await vendorContext.SaveChangesAsync(cancellationToken);

        po.UpdateDraftDetails(
            450m, new DateOnly(2026, 12, 1), 2, 40m,
            latestAcceptableDate: new DateOnly(2026, 12, 15), overTolerancePercent: 5m, underTolerancePercent: 5m,
            fabricResponsibilityId: 1, lines);
        po.Send();
        await vendorContext.SaveChangesAsync(cancellationToken);

        po.Acknowledge();
        await vendorContext.SaveChangesAsync(cancellationToken);

        // The vendor comes back asking for a higher price and a later delivery (Pending, vendor-initiated).
        po.CreateRevision(
            initiatorId: 2, reasonId: 1, impactNote: "Sample: fabric price rose after acknowledgement.",
            vendorMessage: "Fabric costs rose - we can deliver at 480 per piece if delivery moves to 6 December.",
            unitCost: 480m, expectedDeliveryDate: new DateOnly(2026, 12, 6), latestAcceptableDate: new DateOnly(2026, 12, 15),
            overTolerancePercent: 5m, underTolerancePercent: 5m, paymentTermId: 2, advancePercent: 40m, fabricResponsibilityId: 1,
            lines, goesImmediatelyInForce: false);
        await vendorContext.SaveChangesAsync(cancellationToken);
    }
}
