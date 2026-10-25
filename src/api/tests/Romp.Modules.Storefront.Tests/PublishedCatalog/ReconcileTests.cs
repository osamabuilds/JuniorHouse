using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog task 14: <see cref="ReconcileCommand"/> repairs what lost events left behind (PCT-5) and keeps lookup copies current.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class ReconcileTests(StorefrontPostgres postgres)
{
    private static async Task<StorefrontHost> HostWithAsync(StorefrontPostgres postgres, params PublishedProductSnapshot[] products)
    {
        var host = await StorefrontHost.CreateAsync(postgres);
        foreach (var p in products)
        {
            host.Producers.Products[p.ProductId] = p;
        }

        return host;
    }

    private static Task<List<long>> ProductIdsAsync(StorefrontHost host) =>
        host.QueryAsync(db => db.Products.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync());

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Reconcile_ProductWhoseEventWasLost_IsFetchedAndProjected()
    {
        using var host = await HostWithAsync(postgres, Snapshots.Product(1));

        var result = await host.SendAsync(new ReconcileCommand());

        Assert.Equal(1, result.Refreshed);
        Assert.Equal([1L], await ProductIdsAsync(host));
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Reconcile_StoredProductOlderThanCatalogs_IsRefreshed_AndAnUpToDateOneIsLeftAlone()
    {
        using var host = await HostWithAsync(postgres, Snapshots.Product(1), Snapshots.Product(2));
        await host.SendAsync(new ReconcileCommand());
        host.Producers.Products[1] = Snapshots.Product(1, version: 2, name: "Renamed");

        var result = await host.SendAsync(new ReconcileCommand());

        Assert.Equal(1, result.Refreshed);
        Assert.Equal(0, result.Removed);
        Assert.Equal("Renamed", (await host.QueryAsync(db => db.Products.AsNoTracking().SingleAsync(p => p.Id == 1))).Name);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Reconcile_ProductCatalogNoLongerPublishes_IsRemoved_WhereverItSitsInThePaging()
    {
        using var host = await HostWithAsync(postgres, [.. Enumerable.Range(1, 6).Select(i => Snapshots.Product(i))]);
        await host.SendAsync(new ReconcileCommand());
        host.Producers.Products.Remove(3); // in the middle of a page
        host.Producers.Products.Remove(6); // beyond the last published id

        var result = await host.SendAsync(new ReconcileCommand(PageSize: 2));

        Assert.Equal(2, result.Removed);
        Assert.Equal([1L, 2, 4, 5], await ProductIdsAsync(host));
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Reconcile_ManyPages_VisitsEveryProduct()
    {
        using var host = await HostWithAsync(postgres, [.. Enumerable.Range(1, 7).Select(i => Snapshots.Product(i))]);

        var result = await host.SendAsync(new ReconcileCommand(PageSize: 3));

        Assert.Equal(7, result.Refreshed);
        Assert.Equal(7, (await ProductIdsAsync(host)).Count);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Reconcile_NothingToRepair_ChangesNothing()
    {
        using var host = await HostWithAsync(postgres, Snapshots.Product(1));
        await host.SendAsync(new ReconcileCommand());

        var result = await host.SendAsync(new ReconcileCommand());

        Assert.Equal(0, result.Refreshed);
        Assert.Equal(0, result.Removed);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Reconcile_LostStockEvent_IsAppliedToTheProduct()
    {
        using var host = await HostWithAsync(postgres, Snapshots.Product(1, variants: [Snapshots.Variant(11, "SKU-A")]));
        host.Producers.Stock[77] = new StockAvailability(77, "SKU-A", 9, 3);

        await host.SendAsync(new ReconcileCommand());

        Assert.True((await host.QueryAsync(db => db.Products.AsNoTracking().SingleAsync())).IsAvailable);
        Assert.Equal(3, (await host.QueryAsync(db => db.Stock.AsNoTracking().SingleAsync())).StockVersion);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Reconcile_Charts_AreAddedAndRemoved()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var chart = new PublishedSizeChart(
            4, 1, new PublishedCode(2, "DRESSES", "Dresses"), new PublishedCode(1, "GIRLS", "Girls"), "Chart",
            [new PublishedSizeChartLine(new PublishedCode(9, "5-6Y", "5-6Y"), 60, 83, null, null, null, null, null, null, null)]);
        host.Producers.Charts[(2, 1)] = chart;
        await host.SendAsync(new ReconcileCommand());
        Assert.Equal(1, await host.QueryAsync(db => db.Charts.CountAsync()));

        host.Producers.Charts.Clear();
        var result = await host.SendAsync(new ReconcileCommand());

        Assert.Equal(1, result.Removed);
        Assert.Equal(0, await host.QueryAsync(db => db.Charts.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task Reconcile_RenamedLookups_ReachTheRows_AndTheRefreshTimeIsStored()
    {
        using var host = await HostWithAsync(postgres, Snapshots.Product(1, variants: [Snapshots.Variant(11, "SKU-A")]));
        await host.SendAsync(new ReconcileCommand());
        host.Lookups.Categories.Add(new CatalogLookupItem(2, "DRESSES", "Frocks", 1, true));
        host.Lookups.Sizes[0] = new CatalogLookupItem(9, "5-6Y", "Five to six", 30, true);

        var result = await host.SendAsync(new ReconcileCommand());

        Assert.True(result.LookupCopiesChanged >= 2);
        await host.QueryAsync(async db =>
        {
            Assert.Equal("Frocks", (await db.Products.AsNoTracking().SingleAsync()).CategoryName);
            var variant = await db.Variants.AsNoTracking().SingleAsync();
            Assert.Equal("Five to six", variant.SizeName);
            Assert.Equal(30, variant.SizeSeq);
            Assert.NotNull((await db.ModelMeta.AsNoTracking().SingleAsync()).LookupsUpdatedAt);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task Reconcile_LookupsAlreadyCurrent_ChangeNoCopies()
    {
        using var host = await HostWithAsync(postgres, Snapshots.Product(1));
        host.Lookups.Categories.Add(new CatalogLookupItem(2, "DRESSES", "Dresses", 1, true));
        await host.SendAsync(new ReconcileCommand());

        Assert.Equal(0, (await host.SendAsync(new ReconcileCommand())).LookupCopiesChanged);
    }
}
