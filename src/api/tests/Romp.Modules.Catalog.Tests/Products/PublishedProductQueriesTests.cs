using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.Products;

/// <summary>
/// IPublishedProductQueries (published-catalog task 4): the snapshots the storefront read model is built from (PCT-1, 2, 5, 6, 8).
/// Real PostgreSQL; Reference and Labelling are the host's fakes.
/// </summary>
[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class PublishedProductQueriesTests(CatalogPostgres postgres)
{
    private static async Task<ProductDto> PublishedAsync(CatalogHost host, params (short Colour, short Size)[] labelled)
    {
        var product = await host.MakePublishableAsync(await host.NewProductAsync(), labelled.Length == 0 ? [(1, 4), (2, 5)] : labelled);
        await host.SendAsync(new PublishProductCommand(product.Id, product.Version, "Test Person"));
        return await host.SendAsync(new GetProductQuery(product.Id));
    }

    private static Task<T> QueriesAsync<T>(CatalogHost host, Func<IPublishedProductQueries, Task<T>> action) =>
        host.InScopeAsync(sp => action(sp.GetRequiredService<IPublishedProductQueries>()));

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task APublishedProduct_HasASnapshotWithEverythingThePageShows()
    {
        using var host = new CatalogHost(postgres);
        var product = await PublishedAsync(host);
        await host.SendAsync(new SetCollectionsCommand(product.Id, [1, 2], (await host.SendAsync(new GetProductQuery(product.Id))).Version, "Test Person"));

        var snapshot = await QueriesAsync(host, q => q.GetSnapshotAsync(product.Id, CancellationToken.None));

        Assert.NotNull(snapshot);
        Assert.Equal(product.Id, snapshot.ProductId);
        Assert.Equal(product.Path, snapshot.Path);
        Assert.Equal(product.Name, snapshot.Name);
        Assert.Equal(product.StyleCode, snapshot.StyleCode);
        Assert.Equal("GIRLS", snapshot.Gender.Code);
        Assert.Equal("Girls", snapshot.Gender.Name);
        Assert.Equal(["NEW-ARRIVALS", "SALE"], snapshot.Collections.Select(c => c.Code).Order());
        Assert.Equal(product.PublishedAt, snapshot.PublishedAt);

        var sellable = snapshot.Variants.Where(v => v.IsSellable).ToList();
        Assert.Equal(2, sellable.Count);
        Assert.All(sellable, v => Assert.StartsWith("ROMP-S", v.SkuCode, StringComparison.Ordinal));
        Assert.Equal(6, snapshot.Variants.Count);
        Assert.All(snapshot.Variants.Where(v => !v.IsSellable), v => Assert.Null(v.SkuCode));

        var image = Assert.Single(snapshot.Images);
        Assert.Equal("A red floral summer dress, front view", image.AltText);
        Assert.Equal((40, 60), (image.Width, image.Height));
        Assert.Equal(["COTTON", "ELASTANE"], snapshot.Composition.Select(c => c.Fibre.Code).Order());
        Assert.Equal([95m, 5m], snapshot.Composition.Select(c => c.Percent).OrderDescending());
        Assert.Equal(["NO-BLEACH", "WASH-30"], snapshot.Care.Select(c => c.Care.Code).Order());
        Assert.Contains(snapshot.Care, c => c.SymbolCode == "wash-30");
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task TheSnapshotVersion_IsTheProductsContentVersion_AndMovesWithEveryChange()
    {
        using var host = new CatalogHost(postgres);
        var product = await PublishedAsync(host);
        var first = await QueriesAsync(host, q => q.GetSnapshotAsync(product.Id, CancellationToken.None));

        var current = await host.SendAsync(new GetProductQuery(product.Id));
        await host.SendAsync(new SetVariantPricesCommand(product.Id, [new VariantPriceInput(1, 4, 1500m, 1800m)], current.Version, "Test Person"));
        var second = await QueriesAsync(host, q => q.GetSnapshotAsync(product.Id, CancellationToken.None));

        Assert.Equal(first!.Version + 1, second!.Version);
        Assert.Equal(1500m, second.Variants.Single(v => v.Colour.Id == 1 && v.Size.Id == 4).Price);
        Assert.Equal(1800m, second.Variants.Single(v => v.Colour.Id == 1 && v.Size.Id == 4).CompareAtPrice);
    }

    [Fact]
    [Trait("Spec", "PCT-2")]
    public async Task ADraftAnUnpublishedAndAnArchivedProduct_HaveNoSnapshot()
    {
        using var host = new CatalogHost(postgres);
        var draft = await host.NewProductAsync();
        var unpublished = await PublishedAsync(host);
        await host.SendAsync(new UnpublishProductCommand(unpublished.Id, (await host.SendAsync(new GetProductQuery(unpublished.Id))).Version, "Test Person"));
        var archived = await PublishedAsync(host);
        await host.SendAsync(new ArchiveProductCommand(archived.Id, (await host.SendAsync(new GetProductQuery(archived.Id))).Version, "Test Person"));

        foreach (var id in new[] { draft.Id, unpublished.Id, archived.Id })
        {
            Assert.Null(await QueriesAsync(host, q => q.GetSnapshotAsync(id, CancellationToken.None)));
        }

        Assert.Null(await QueriesAsync(host, q => q.GetSnapshotAsync(long.MaxValue, CancellationToken.None)));
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task ListVersions_AndListSnapshots_PagePublishedProductsByIdWithoutRepeatsOrGaps()
    {
        using var host = new CatalogHost(postgres);
        var ids = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await PublishedAsync(host)).Id);
        }

        await host.NewProductAsync(); // a draft in between must not appear
        var after = ids[0] - 1;

        var firstPage = await QueriesAsync(host, q => q.ListSnapshotsAsync(after, 2, CancellationToken.None));
        var secondPage = await QueriesAsync(host, q => q.ListSnapshotsAsync(firstPage[^1].ProductId, 2, CancellationToken.None));
        var versions = await QueriesAsync(host, q => q.ListVersionsAsync(after, 100, CancellationToken.None));

        Assert.Equal(ids.Take(2), firstPage.Select(s => s.ProductId));
        Assert.Equal(ids[2], secondPage[0].ProductId);
        Assert.Equal(ids, versions.Where(v => v.ProductId >= ids[0]).Take(3).Select(v => v.ProductId));
        Assert.Equal(versions.OrderBy(v => v.ProductId).Select(v => v.ProductId), versions.Select(v => v.ProductId));
        var snapshots = firstPage.Concat(secondPage).ToDictionary(s => s.ProductId);
        Assert.All(versions.Where(v => snapshots.ContainsKey(v.ProductId)), v => Assert.Equal(snapshots[v.ProductId].Version, v.Version));
    }

    [Fact]
    [Trait("Spec", "PCT-8")]
    public async Task ASnapshotPage_CostsTheSameNumberOfQueries_HoweverManyProductsItHolds()
    {
        var counter = new CommandCounter();
        using var host = new CatalogHost(postgres, counter: counter);
        var firstId = (await PublishedAsync(host)).Id;
        counter.Reset();
        await QueriesAsync(host, q => q.ListSnapshotsAsync(firstId - 1, 1, CancellationToken.None));
        var forOne = counter.Count;

        for (var i = 0; i < 3; i++)
        {
            await PublishedAsync(host);
        }

        counter.Reset();
        var page = await QueriesAsync(host, q => q.ListSnapshotsAsync(firstId - 1, 50, CancellationToken.None));
        var forMany = counter.Count;

        Assert.True(page.Count >= 4);
        Assert.Equal(forOne, forMany);
    }

    [Fact]
    [Trait("Spec", "ADR-0011")]
    public void TheContract_ExposesNeitherGtinsNorStaffOrAuditData()
    {
        var names = typeof(PublishedProductSnapshot).Assembly.GetTypes()
            .Where(t => t.Name.StartsWith("Published", StringComparison.Ordinal))
            .SelectMany(t => t.GetProperties().Select(p => p.Name))
            .ToList();

        Assert.NotEmpty(names);
        Assert.DoesNotContain(names, n => n.Contains("Gtin", StringComparison.OrdinalIgnoreCase)
            || n.EndsWith("By", StringComparison.Ordinal)
            || n.StartsWith("Insr", StringComparison.Ordinal)
            || n.StartsWith("Updt", StringComparison.Ordinal));
    }
}
