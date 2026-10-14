using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Api.Seeding;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Production.Infrastructure.Persistence;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Infrastructure.Persistence;
using Romp.Modules.Vendor.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Quality;

/// <summary>SCRUM-197: the demo data has nine delivered POs, one in each receiving and QC state, is idempotent, and its events flow without a dead letter.</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class DemoQualitySeederTests(ProductionApiFixture api)
{
    [Fact]
    [Trait("Spec", "demo")]
    public async Task Seed_Twice_NoDuplicates_AndEveryQcStateIsRepresented()
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
                await DemoQualitySeeder.SeedAsync(
                    catalog, scope.ServiceProvider.GetRequiredService<VendorDbContext>(), scope.ServiceProvider.GetRequiredService<ProductionDbContext>(),
                    scope.ServiceProvider.GetRequiredService<QcDbContext>(), CancellationToken.None);
            }
        }

        await api.DispatchAsync(); // the seeded events reach their consumers

        await using var check = api.Services.CreateAsyncScope();
        var quality = check.ServiceProvider.GetRequiredService<QcDbContext>();
        var vendor = check.ServiceProvider.GetRequiredService<VendorDbContext>();

        Assert.Equal(9, await vendor.PurchaseOrders.CountAsync(p => p.PoNo.StartsWith("PO-2026-9100")));
        Assert.Equal(9, await quality.GoodsReceipts.CountAsync(g => g.PoNo.StartsWith("PO-2026-9100"))); // seeded once, not twice
        Assert.Equal(9, await quality.Batches.CountAsync(b => b.PoNo.StartsWith("PO-2026-9100")));
        Assert.Equal(9, await quality.Deliveries.CountAsync(d => d.PoNo.StartsWith("PO-2026-9100"))); // the consumer found the copies and added none

        var batches = await quality.Batches.AsNoTracking().Include(b => b.SubBatches).Include(b => b.Inspections)
            .Where(b => b.PoNo.StartsWith("PO-2026-9100")).OrderBy(b => b.PoNo).ToListAsync();
        Assert.Equal(
            [BatchStatus.Pending, BatchStatus.Decided, BatchStatus.Decided, BatchStatus.Decided, BatchStatus.AwaitingRework, BatchStatus.Decided, BatchStatus.Decided, BatchStatus.Decided, BatchStatus.InInspection],
            batches.Select(b => b.StatusId).ToArray());

        var reconciliation = await quality.GoodsReceipts.AsNoTracking().Where(g => g.PoNo.StartsWith("PO-2026-9100")).OrderBy(g => g.PoNo).Select(g => g.ReconciliationId).ToListAsync();
        Assert.Equal(GrnReconciliation.Shortage, reconciliation[1]); // 91002 counted short
        Assert.Equal(GrnReconciliation.Over, reconciliation[8]); // 91009 over-delivery
        Assert.Equal(7, reconciliation.Count(r => r == GrnReconciliation.Matched));

        // Rework: 91005 still has a part with the vendor; 91006's came back and was accepted.
        Assert.Single(batches[4].SubBatches, s => s.OutcomeId == QcOutcome.Rework && s.StatusId == BatchStatus.AwaitingRework);
        Assert.Contains(batches[5].SubBatches, s => s.ParentSubBatchId is not null && s.OutcomeId == QcOutcome.Accepted);

        // The released quantity of the demo POs follows the outcomes: rejected and rework quantity is never in it.
        var queries = check.ServiceProvider.GetRequiredService<IQualityQueries>();
        var releasedByPo = new Dictionary<string, int>();
        foreach (var batch in batches)
        {
            releasedByPo[batch.PoNo] = (await queries.GetReleasedQuantitiesByBatchAsync(batch.Id, CancellationToken.None)).Sum(l => l.Qty);
        }

        Assert.Equal(
            [0, 140, 145, 0, 105, 145, 115, 90, 0],
            releasedByPo.OrderBy(p => p.Key).Select(p => p.Value).ToArray());

        // Every seeded event was handled by its consumers: nothing is left waiting or dead-lettered.
        var health = await api.Services.GetRequiredService<OutboxHealthReader>().ReadAsync(CancellationToken.None);
        Assert.All(health, h => Assert.Equal(0, h.DeadLettered));
        Assert.All(health, h => Assert.Equal(0, h.Pending));
    }
}
