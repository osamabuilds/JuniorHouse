using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.Seeding;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Production.Infrastructure.Persistence;
using Romp.Modules.Vendor.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Production;

/// <summary>SCRUM-194: the demo data has one purchase order in each production state, is idempotent, and its events flow without a dead letter.</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class DemoDataSeederTests(ProductionApiFixture api)
{
    [Fact]
    [Trait("Spec", "demo")]
    public async Task Seed_Twice_NoDuplicates_AndEveryProductionStateIsRepresented()
    {
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            if (!await catalog.Styles.AnyAsync(s => s.Code == "STY-DRESS-001"))
            {
                var dress = new Style("STY-DRESS-001", "Floral Summer Dress", "Summer 2026", 3, 2, 3, 1, 450m, 1200m);
                dress.SetColourSizeAndTargets([1, 2], [4, 5], [(4, 1, 50), (4, 2, 40), (5, 1, 30), (5, 2, 25)]);
                catalog.Styles.Add(dress);
                await catalog.SaveChangesAsync();
            }

            for (var pass = 0; pass < 2; pass++)
            {
                await DemoProductionSeeder.SeedAsync(
                    catalog, scope.ServiceProvider.GetRequiredService<VendorDbContext>(),
                    scope.ServiceProvider.GetRequiredService<ProductionDbContext>(), CancellationToken.None);
            }
        }

        await api.DispatchAsync(); // the seeded events reach their consumers

        await using var check = api.Services.CreateAsyncScope();
        var vendor = check.ServiceProvider.GetRequiredService<VendorDbContext>();
        var production = check.ServiceProvider.GetRequiredService<ProductionDbContext>();

        var demoPos = await vendor.PurchaseOrders.AsNoTracking().Where(p => p.PoNo.StartsWith("PO-2026-9000") && p.PoNo != "PO-2026-90001")
            .OrderBy(p => p.PoNo).Select(p => new { p.PoNo, p.StatusId }).ToListAsync();
        Assert.Equal(6, demoPos.Count); // seeded once, not twice
        Assert.Equal([3, 3, 5, 6, 7, 4], demoPos.Select(p => (int)p.StatusId).ToArray());

        var runs = await production.Runs.AsNoTracking().Where(r => r.PoNo.StartsWith("PO-2026-9000") && r.PoNo != "PO-2026-90001").OrderBy(r => r.PoNo).ToListAsync();
        Assert.Equal(6, runs.Count);
        Assert.Equal([1, 1, 1, 1, 1, 2], runs.Select(r => (int)r.StatusId).ToArray()); // the last one was cancelled in production

        var shortNote = await production.DeliveryNotes.AsNoTracking().SingleAsync(n => n.VendorDnNo == "DN-DEMO-2" && n.IsFinalShipment);
        Assert.Equal(23, shortNote.ShortQty);

        // Every seeded event was handled by its consumers: nothing is left waiting or dead-lettered in either outbox.
        var reader = api.Services.GetRequiredService<OutboxHealthReader>();
        var health = await reader.ReadAsync(CancellationToken.None);
        Assert.All(health, h => Assert.Equal(0, h.DeadLettered));
        Assert.All(health, h => Assert.Equal(0, h.Pending));
    }
}
