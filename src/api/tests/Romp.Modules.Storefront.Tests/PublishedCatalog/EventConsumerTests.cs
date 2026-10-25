using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Storefront.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog task 13: the Catalog and Warehouse event consumers - current state is read through the contract, and the inbox makes a redelivery a no-op.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class EventConsumerTests(StorefrontPostgres postgres)
{
    private static ProductPublishedEvent Published(long id = 1) => new(id, 1, $"/girls/dresses/floral-{id}", 1);

    private static StockLevelChangedEvent Level(long variantId, string sku, int available, long version) =>
        new(variantId, 1, 1, 1, sku, "GTIN", available, available, available, 0, "PUTAWAY", version);

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task ProductPublished_ReadsTheSnapshotAndProjectsIt()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        host.Producers.Products[1] = Snapshots.Product();

        await host.DeliverAsync(1, Published());

        Assert.Equal("Floral dress", (await host.QueryAsync(db => db.Products.AsNoTracking().SingleAsync())).Name);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task SameMessageDeliveredTenTimes_GivesOneState_AndReadsTheProducerOnce()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        host.Producers.Products[1] = Snapshots.Product();

        for (var i = 0; i < 10; i++)
        {
            await host.DeliverAsync(5, Published());
        }

        Assert.Equal(1, host.Producers.Reads);
        await host.QueryAsync(async db =>
        {
            Assert.Equal(1, await db.Products.CountAsync());
            Assert.Equal(8, await db.Derivatives.CountAsync());
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task OldEventDeliveredLate_ProjectsTheCurrentStateNotTheEventsMoment()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        host.Producers.Products[1] = Snapshots.Product(version: 3, name: "Current");

        await host.DeliverAsync(1, new ProductChangedEvent(1, "/girls/dresses/floral-1", null, 2));

        Assert.Equal("Current", (await host.QueryAsync(db => db.Products.AsNoTracking().SingleAsync())).Name);
    }

    [Theory]
    [Trait("Spec", "PCT-2")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductUnpublishedOrArchived_RemovesTheProduct(bool archived)
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        host.Producers.Products[1] = Snapshots.Product();
        await host.DeliverAsync(1, Published());
        host.Producers.Products.Clear(); // Catalog no longer publishes it

        if (archived)
        {
            await host.DeliverAsync(2, new ProductArchivedEvent(1, 1, "/girls/dresses/floral-1", 2));
        }
        else
        {
            await host.DeliverAsync(2, new ProductUnpublishedEvent(1, 1, "/girls/dresses/floral-1", 2));
        }

        Assert.Equal(0, await host.QueryAsync(db => db.Products.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    public async Task StockEvents_ReadWarehousesCurrentFigure_AndUpdateTheProduct()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        host.Producers.Products[1] = Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A")]);
        await host.DeliverAsync(1, Published());
        host.Producers.Stock[77] = new StockAvailability(77, "SKU-A", 6, 1);

        await host.DeliverAsync(2, Level(77, "SKU-A", 6, 1));
        Assert.True((await host.QueryAsync(db => db.Products.AsNoTracking().SingleAsync())).IsAvailable);

        host.Producers.Stock[77] = new StockAvailability(77, "SKU-A", 0, 2);
        await host.DeliverAsync(3, new StockPutawayConfirmedEvent(77, 1, 1, 1, "SKU-A", "GTIN", "A", 1, 0, 2));
        Assert.False((await host.QueryAsync(db => db.Products.AsNoTracking().SingleAsync())).IsAvailable);
    }

    [Fact]
    [Trait("Spec", "PCT-9")]
    public async Task StockEventForAVariantWarehouseHasNoRowFor_ChangesNothing()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await host.DeliverAsync(1, Level(77, "SKU-A", 6, 1));

        Assert.Equal(0, await host.QueryAsync(db => db.Stock.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task SizeChartChanged_ProjectsTheChart_AndRemovesItWhenTheAnswerIsAnotherChart()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var chart = new PublishedSizeChart(
            4, 1, new PublishedCode(2, "DRESSES", "Dresses"), new PublishedCode(1, "GIRLS", "Girls"), "Chart",
            [new PublishedSizeChartLine(new PublishedCode(9, "5-6Y", "5-6Y"), 60, 83, null, null, null, null, null, null, null)]);
        host.Producers.Charts[(2, 1)] = chart;

        await host.DeliverAsync(1, new SizeChartChangedEvent(4, 2, 1, 1));
        Assert.Equal(1, await host.QueryAsync(db => db.Charts.CountAsync()));

        // the chart was deleted and the category now answers with the Unisex fallback, a different chart
        host.Producers.Charts[(2, 1)] = chart with { ChartId = 9, Version = 2 };
        await host.DeliverAsync(2, new SizeChartChangedEvent(4, 2, 1, 2));
        Assert.Equal(0, await host.QueryAsync(db => db.Charts.CountAsync()));
    }
}
