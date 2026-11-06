using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>
/// sold-items-in-listings task 13: rows hidden under the old rule (listable only with stock) come back on a rebuild, with the in-stock
/// flag untouched (SLD-13). Real PostgreSQL.
/// </summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class RebuildSoldItemsTests(StorefrontPostgres postgres)
{
    [Fact]
    [Trait("Spec", "SLD-13")]
    public async Task Rebuild_BringsBackASoldOutProductTheOldRuleHid_StillMarkedOutOfStock()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        host.Storage.Files["images/a.jpg"] = [1, 2, 3];
        host.Producers.Products[1] = Snapshots.Product(1, variants: [Snapshots.Variant(11, "SKU-A", price: 800m)], images: [Snapshots.Image(101, 1)]);
        host.Producers.Stock[11] = new StockAvailability(11, "SKU-A", 0, 1);
        await host.SendAsync(new ReconcileCommand());
        while (await host.SendAsync(new ProcessDerivativeCommand()) != DerivativeOutcome.NothingDue)
        {
        }

        // The row as the old rule left it: picture ready, nothing in stock, hidden.
        await host.QueryAsync(db => db.Products.ExecuteUpdateAsync(s => s.SetProperty(p => p.IsListable, false)));
        var runId = await host.SendAsync(new StartRebuildCommand("test"));

        await host.SendAsync(new RebuildStorefrontCommand(runId));

        var product = await host.QueryAsync(db => db.Products.AsNoTracking().SingleAsync());
        Assert.True(product.IsListable);
        Assert.False(product.IsAvailable);
    }
}
