using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Contracts;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;
using Romp.Modules.Storefront.Tests.Support;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>customers/wishlist W2 (WSL-5, WSL-8): product summaries for another module, in a constant number of queries. Real PostgreSQL.</summary>
public sealed class StorefrontCatalogQueriesTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private static T Stamp<T>(T entity)
        where T : IAuditable
    {
        entity.InsrDte = Now;
        entity.InsrBy = "test";
        return entity;
    }

    private static PublishedProduct Product(long id, decimal? min, bool onSale, bool available)
    {
        var product = Stamp(new PublishedProduct(
            id, 1, $"/girls/dresses/dress-{id}", $"Dress {id}", "A dress.", $"STY-{id}", "GIRLS", "Girls", "DRESSES", "Dresses", "KIDS", "Kids 4-8Y", Now, null));
        if (min is { } price)
        {
            product.SetDerived(
                new PriceSummary(price, price, onSale, onSale ? 25m : 0m),
                new ProductVisibilityResult(available, available),
                imageReady: true);
        }

        return product;
    }

    private async Task<StfrDbContext> SeedAsync(QueryCounter? counter = null)
    {
        await using (var setup = StorefrontPostgres.NewContext(_postgres.GetConnectionString()))
        {
            await setup.Database.MigrateAsync();
            setup.Products.AddRange(
                Product(1, 1500m, onSale: true, available: true),
                Product(2, 2000m, onSale: false, available: false),
                Product(3, null, onSale: false, available: false));
            await setup.SaveChangesAsync();
            setup.Variants.Add(Stamp(new VariantRow(11, 1, 1, "NAVY", "Navy", 1, "4-5Y", "4-5 years", 1, "SKU-11", 1500m, 2000m, true)));
            setup.Variants.Add(Stamp(new VariantRow(21, 2, 1, "NAVY", "Navy", 1, "4-5Y", "4-5 years", 1, "SKU-21", 2000m, null, true)));
            setup.Images.Add(Stamp(new ImageRow(100, 1, null, "images/100.png", 800, 1000, "alt", 1, true)));
            setup.Images.Add(Stamp(new ImageRow(101, 1, null, "images/101.png", 800, 1000, "alt", 2, false)));
            await setup.SaveChangesAsync();
        }

        return StorefrontPostgres.NewContext(_postgres.GetConnectionString(), counter);
    }

    private async Task<StfrDbContext> SeedVariantsAsync(QueryCounter? counter = null)
    {
        await using (var setup = StorefrontPostgres.NewContext(_postgres.GetConnectionString()))
        {
            await setup.Database.MigrateAsync();
            setup.Products.AddRange(Product(1, 1500m, onSale: false, available: true), Product(3, null, onSale: false, available: false));
            await setup.SaveChangesAsync();
            var inStock = Stamp(new VariantRow(11, 1, 1, "NAVY", "Navy", 1, "4-5Y", "4-5 years", 1, "SKU-11", 1500m, null, true));
            inStock.ApplyStock(7, 1);
            var soldOut = Stamp(new VariantRow(12, 1, 1, "NAVY", "Navy", 2, "6-7Y", "6-7 years", 2, "SKU-12", 1500m, null, true));
            soldOut.ApplyStock(0, 1);
            var noStockYet = Stamp(new VariantRow(13, 1, 1, "NAVY", "Navy", 3, "8-9Y", "8-9 years", 3, "SKU-13", 1500m, null, true));
            var notSellable = Stamp(new VariantRow(14, 1, 1, "NAVY", "Navy", 4, "10-11Y", "10-11 years", 4, "SKU-14", 1500m, null, false));
            var unpublishedProduct = Stamp(new VariantRow(31, 3, 1, "NAVY", "Navy", 1, "4-5Y", "4-5 years", 1, "SKU-31", null, null, true));
            setup.Variants.AddRange(inStock, soldOut, noStockYet, notSellable, unpublishedProduct);
            await setup.SaveChangesAsync();
        }

        return StorefrontPostgres.NewContext(_postgres.GetConnectionString(), counter);
    }

    [Fact]
    [Trait("Spec", "NTM-1")]
    public async Task Variants_AreReturnedWithAvailability_AndUnknownUnsellableAndUnpublishedAreAbsent()
    {
        await using var context = await SeedVariantsAsync();

        var result = await new StorefrontCatalogQueries(context).GetVariantSummariesAsync([11, 12, 13, 14, 31, 999, 0, -4], CancellationToken.None);

        Assert.Equal([11L, 12L, 13L], result.Select(v => v.VariantId));
        Assert.Equal([true, false, false], result.Select(v => v.IsAvailable));
        Assert.Equal(new VariantSummary(12, 1, "Dress 1", "/girls/dresses/dress-1", "Navy", "6-7 years", "SKU-12", false), result[1]);
    }

    [Fact]
    [Trait("Spec", "NTM-13")]
    public async Task ManyVariants_AreOneQuery_AndTheLimitIsEnforced()
    {
        var counter = new QueryCounter();
        await using var context = await SeedVariantsAsync(counter);
        var queries = new StorefrontCatalogQueries(context);
        counter.Reset();

        var all = await queries.GetVariantSummariesAsync([11, 12, 13], CancellationToken.None);

        Assert.Equal(3, all.Count);
        Assert.Equal(1, counter.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => queries.GetVariantSummariesAsync(
            Enumerable.Range(1, IStorefrontCatalogQueries.MaxIds + 1).Select(i => (long)i).ToList(), CancellationToken.None));
    }

    [Fact]
    [Trait("Spec", "WSL-5")]
    public async Task ASaleProduct_ComesBackWithItsPriceWasPriceAndMainImage()
    {
        await using var context = await SeedAsync();

        var summary = Assert.Single(await new StorefrontCatalogQueries(context).GetProductSummariesAsync([1], CancellationToken.None));

        Assert.Equal(new ProductSummary(1, "Dress 1", "/girls/dresses/dress-1", 1500m, 2000m, true, 100), summary);
    }

    [Fact]
    [Trait("Spec", "WSL-5")]
    public async Task ASoldOutProduct_IsReturnedUnavailable_ButUnknownAndUnpricedIdsAreAbsent()
    {
        await using var context = await SeedAsync();

        var result = await new StorefrontCatalogQueries(context).GetProductSummariesAsync([2, 3, 999, -1, 0], CancellationToken.None);

        var summary = Assert.Single(result);
        Assert.Equal(2, summary.ProductId);
        Assert.False(summary.IsAvailable);
        Assert.Null(summary.WasPrice);
        Assert.Null(summary.MainImageId);
    }

    [Fact]
    [Trait("Spec", "WSL-8")]
    public async Task ManyProducts_AreAConstantNumberOfQueries_AndTheLimitIsEnforced()
    {
        var counter = new QueryCounter();
        await using var context = await SeedAsync(counter);
        var queries = new StorefrontCatalogQueries(context);
        counter.Reset();

        var both = await queries.GetProductSummariesAsync([1, 2], CancellationToken.None);

        Assert.Equal([1L, 2L], both.Select(p => p.ProductId).Order());
        Assert.True(counter.Count <= 3, $"expected at most 3 queries, ran {counter.Count}");
        await Assert.ThrowsAsync<ArgumentException>(() => queries.GetProductSummariesAsync(
            Enumerable.Range(1, IStorefrontCatalogQueries.MaxIds + 1).Select(i => (long)i).ToList(), CancellationToken.None));
    }
}
