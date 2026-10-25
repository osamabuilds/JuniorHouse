using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog task 11: <see cref="ApplyAvailabilityCommand"/> against a real PostgreSQL - version-wins stock, the holding area, and the product flags.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class ApplyAvailabilityTests(StorefrontPostgres postgres)
{
    private static ApplyAvailabilityCommand Stock(string sku, int available, long version, long variantId = 77) =>
        new(new StockAvailability(variantId, sku, available, version));

    private static Task<PublishedProduct> ProductAsync(StorefrontHost host) =>
        host.QueryAsync(db => db.Products.AsNoTracking().SingleAsync());

    [Fact]
    [Trait("Spec", "PCT-3")]
    public async Task Apply_StockForAPublishedVariant_MakesTheProductAvailableAndPriced()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A", price: 800m, compareAt: 1000m)])));
        Assert.False((await ProductAsync(host)).IsAvailable);

        var outcome = await host.SendAsync(Stock("SKU-A", 20, 1));

        Assert.Equal(AvailabilityOutcome.Applied, outcome);
        var product = await ProductAsync(host);
        Assert.True(product.IsAvailable);
        Assert.Equal(800m, product.MinPrice);
        Assert.True(product.IsOnSale);
        Assert.Equal(20, (await host.QueryAsync(db => db.Variants.AsNoTracking().SingleAsync())).AvailableQuantity);
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    public async Task Apply_StockDroppingToZero_MakesTheProductUnavailable()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A")])));
        await host.SendAsync(Stock("SKU-A", 5, 1));
        Assert.True((await ProductAsync(host)).IsAvailable);

        await host.SendAsync(Stock("SKU-A", 0, 2));

        Assert.False((await ProductAsync(host)).IsAvailable);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Apply_StockBeforeTheProduct_IsHeld_ThenAppliedWhenTheProductArrives()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var outcome = await host.SendAsync(Stock("SKU-A", 12, 3));
        Assert.Equal(AvailabilityOutcome.Held, outcome);
        Assert.Equal(1, await host.QueryAsync(db => db.Stock.CountAsync()));

        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A")])));

        Assert.True((await ProductAsync(host)).IsAvailable);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Apply_SameOrOlderStock_ChangesNothing()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A")])));
        await host.SendAsync(Stock("SKU-A", 8, 5));

        Assert.Equal(AvailabilityOutcome.Unchanged, await host.SendAsync(Stock("SKU-A", 8, 5)));
        Assert.Equal(AvailabilityOutcome.Unchanged, await host.SendAsync(Stock("SKU-A", 0, 4)));

        Assert.Equal(8, (await host.QueryAsync(db => db.Variants.AsNoTracking().SingleAsync())).AvailableQuantity);
        Assert.Equal(8, (await host.QueryAsync(db => db.Stock.AsNoTracking().SingleAsync())).Quantity);
    }

    [Fact]
    [Trait("Spec", "PCT-9")]
    public async Task Apply_NullReport_LeavesTheVariantNotYetAvailable()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product()));

        Assert.Equal(AvailabilityOutcome.Unchanged, await host.SendAsync(new ApplyAvailabilityCommand(null)));

        Assert.Null((await host.QueryAsync(db => db.Variants.AsNoTracking().SingleAsync())).AvailableQuantity);
        Assert.False((await ProductAsync(host)).IsAvailable);
    }

    [Fact]
    [Trait("Spec", "PCT-11")]
    public async Task Apply_StockForAnUnsellableVariant_StaysNotYetAvailable()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A", sellable: false)])));

        await host.SendAsync(Stock("SKU-A", 10, 1));

        Assert.False((await ProductAsync(host)).IsAvailable);
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    public async Task Apply_SkuCaseDiffers_StillFindsTheVariant()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(variants: [Snapshots.Variant(11, "Sku-A")])));

        Assert.Equal(AvailabilityOutcome.Applied, await host.SendAsync(Stock("SKU-A", 3, 1)));
    }
}
