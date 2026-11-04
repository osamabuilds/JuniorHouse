using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.Products;

/// <summary>IVariantSalesQueries (cart C2): the variant facts a basket and an order read, in one bulk call. Real PostgreSQL.</summary>
[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class VariantSalesQueriesTests(CatalogPostgres postgres)
{
    private static async Task<ProductDto> PublishedAsync(CatalogHost host)
    {
        var product = await host.MakePublishableAsync(await host.NewProductAsync(), [(1, 4), (2, 5)]);
        await host.SendAsync(new PublishProductCommand(product.Id, product.Version, "Test Person"));
        return await host.SendAsync(new GetProductQuery(product.Id));
    }

    private static Task<T> QueriesAsync<T>(CatalogHost host, Func<IVariantSalesQueries, Task<T>> action) =>
        host.InScopeAsync(sp => action(sp.GetRequiredService<IVariantSalesQueries>()));

    private static async Task<IReadOnlyList<long>> VariantIdsAsync(CatalogHost host, long productId, bool sellable)
    {
        var snapshot = await host.InScopeAsync(sp => sp.GetRequiredService<IPublishedProductQueries>().GetSnapshotAsync(productId, CancellationToken.None));
        return snapshot!.Variants.Where(v => v.IsSellable == sellable).Select(v => v.VariantId).ToList();
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task ASellableVariant_ComesBackWithPriceSkuStockKeyAndPicture()
    {
        using var host = new CatalogHost(postgres);
        var product = await PublishedAsync(host);
        var variantId = (await VariantIdsAsync(host, product.Id, sellable: true))[0];

        var rows = await QueriesAsync(host, q => q.GetManyAsync([variantId], CancellationToken.None));

        var sale = Assert.Single(rows);
        Assert.Equal(variantId, sale.VariantId);
        Assert.Equal(product.Id, sale.ProductId);
        Assert.Equal(product.Name, sale.ProductName);
        Assert.Equal(product.Path, sale.Path);
        Assert.True(sale.IsSellable);
        Assert.StartsWith("ROMP-S", sale.SkuCode, StringComparison.Ordinal);
        Assert.NotNull(sale.LabellingVariantId);
        Assert.True(sale.Price > 0);
        Assert.False(string.IsNullOrEmpty(sale.ColourName));
        Assert.False(string.IsNullOrEmpty(sale.SizeName));
        Assert.NotNull(sale.ImageKey);
        Assert.Equal("A red floral summer dress, front view", sale.ImageAlt);
    }

    [Fact]
    [Trait("Spec", "CRT-5")]
    public async Task AVariantWithNoLabelOrPrice_IsReturnedButNotSellable_WithNoSkuOrStockKey()
    {
        using var host = new CatalogHost(postgres);
        var product = await PublishedAsync(host);
        var notSellable = (await VariantIdsAsync(host, product.Id, sellable: false))[0];

        var sale = Assert.Single(await QueriesAsync(host, q => q.GetManyAsync([notSellable], CancellationToken.None)));

        Assert.False(sale.IsSellable);
        Assert.Null(sale.SkuCode);
        Assert.Null(sale.LabellingVariantId);
    }

    [Fact]
    [Trait("Spec", "CRT-5")]
    public async Task VariantsOfADraftAnUnpublishedOrAnUnknownId_AreAbsent()
    {
        using var host = new CatalogHost(postgres);
        var published = await PublishedAsync(host);
        var toUnpublish = await PublishedAsync(host);
        var unpublishedVariants = await VariantIdsAsync(host, toUnpublish.Id, sellable: true);
        await host.SendAsync(new UnpublishProductCommand(toUnpublish.Id, (await host.SendAsync(new GetProductQuery(toUnpublish.Id))).Version, "Test Person"));
        var keep = (await VariantIdsAsync(host, published.Id, sellable: true))[0];

        var rows = await QueriesAsync(host, q => q.GetManyAsync([keep, unpublishedVariants[0], long.MaxValue, -4, 0], CancellationToken.None));

        Assert.Equal(keep, Assert.Single(rows).VariantId);
    }

    [Fact]
    [Trait("Spec", "CRT-8")]
    public async Task APriceChange_IsSeenOnTheNextRead()
    {
        using var host = new CatalogHost(postgres);
        var product = await PublishedAsync(host);
        var current = await host.SendAsync(new GetProductQuery(product.Id));
        await host.SendAsync(new SetVariantPricesCommand(product.Id, [new VariantPriceInput(1, 4, 1500m, 1800m)], current.Version, "Test Person"));
        var snapshot = await host.InScopeAsync(sp => sp.GetRequiredService<IPublishedProductQueries>().GetSnapshotAsync(product.Id, CancellationToken.None));
        var variantId = snapshot!.Variants.Single(v => v.Colour.Id == 1 && v.Size.Id == 4).VariantId;

        var sale = Assert.Single(await QueriesAsync(host, q => q.GetManyAsync([variantId], CancellationToken.None)));

        Assert.Equal(1500m, sale.Price);
        Assert.Equal(1800m, sale.CompareAtPrice);
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task AskingForMoreThanTheLimit_IsRefused()
    {
        using var host = new CatalogHost(postgres);
        var tooMany = Enumerable.Range(1, IVariantSalesQueries.MaxIds + 1).Select(i => (long)i).ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => QueriesAsync(host, q => q.GetManyAsync(tooMany, CancellationToken.None)));
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task TheNumberOfQueries_DoesNotGrowWithTheNumberOfVariants()
    {
        var counter = new CommandCounter();
        using var host = new CatalogHost(postgres, counter: counter);
        var first = await PublishedAsync(host);
        var one = (await VariantIdsAsync(host, first.Id, sellable: true))[0];
        counter.Reset();
        await QueriesAsync(host, q => q.GetManyAsync([one], CancellationToken.None));
        var forOne = counter.Count;

        var many = new List<long> { one };
        for (var i = 0; i < 3; i++)
        {
            many.AddRange(await VariantIdsAsync(host, (await PublishedAsync(host)).Id, sellable: true));
        }

        counter.Reset();
        var rows = await QueriesAsync(host, q => q.GetManyAsync(many, CancellationToken.None));

        Assert.True(rows.Count >= 4);
        Assert.Equal(forOne, counter.Count);
    }

    [Fact]
    [Trait("Spec", "ADR-0011")]
    public void TheContract_ExposesNeitherGtinsNorStaffData()
    {
        var names = typeof(VariantSale).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(names, n => n.Contains("Gtin", StringComparison.OrdinalIgnoreCase) || n.Contains("By", StringComparison.Ordinal) && n.StartsWith("Insr", StringComparison.Ordinal));
    }
}
