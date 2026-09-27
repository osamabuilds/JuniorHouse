using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Domain;
using Romp.Modules.Catalog.Infrastructure;
using Romp.Modules.Vendor.Infrastructure;

namespace Romp.Api;

/// <summary>
/// SCRUM-175: a handful of sample styles and vendors for the "Try Sprint 1" walkthrough (README).
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
            var sialkotVendor = new Romp.Modules.Vendor.Domain.Vendor(
                "Sialkot Garments Co.", "Ali Raza", "+92-300-1234567", "ali@sialkotgarments.pk",
                cityId: 7, paymentTermId: 2);
            sialkotVendor.SetSpecialisations([1, 3]);

            var lahoreVendor = new Romp.Modules.Vendor.Domain.Vendor(
                "Lahore Knitwear Ltd.", "Bilal Khan", "+92-300-7654321", "bilal@lahoreknitwear.pk",
                cityId: 2, paymentTermId: 3);
            lahoreVendor.SetSpecialisations([1]);

            vendorContext.Vendors.AddRange(sialkotVendor, lahoreVendor);
            await vendorContext.SaveChangesAsync(cancellationToken);
        }
    }
}
