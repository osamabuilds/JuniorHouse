using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Application.Urls;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Romp.Modules.Catalog.Tests.Urls;

[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class UrlCommandTests(CatalogPostgres postgres)
{
    private static async Task<ProductDto> PublishedAsync(CatalogHost host, string? name = null)
    {
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(name: name), (1, 4));
        return (await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"))).Product;
    }

    private static Task<PathResolutionResult> ResolveAsync(CatalogHost host, string path) => host.SendAsync(new ResolvePathQuery(path));

    [Fact]
    [Trait("Spec", "URL-4")]
    public async Task TwoProductsWithTheSameName_GetStableDistinctSlugs()
    {
        using var host = new CatalogHost(postgres);
        var name = "Twin Dress " + Guid.NewGuid().ToString("N")[..8];

        var first = await host.NewProductAsync(name: name);
        var second = await host.NewProductAsync(name: name);
        var third = await host.NewProductAsync(name: name);

        Assert.EndsWith(first.Slug + "-2", second.Slug, StringComparison.Ordinal);
        Assert.EndsWith(first.Slug + "-3", third.Slug, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "URL-4")]
    public async Task SuggestSlug_ReturnsTheFirstFreeSlug_AndSkipsAnAddressAnotherProductOnceHad()
    {
        using var host = new CatalogHost(postgres);
        var name = "Suggest Dress " + Guid.NewGuid().ToString("N")[..8];
        var product = await host.NewProductAsync(name: name);

        var taken = await host.SendAsync(new SuggestSlugQuery(name, 2, 3));
        var forItself = await host.SendAsync(new SuggestSlugQuery(name, 2, 3, product.Id));
        var otherGender = await host.SendAsync(new SuggestSlugQuery(name, 1, 3));

        Assert.Equal(product.Slug + "-2", taken.Slug);
        Assert.Equal(product.Slug, forItself.Slug);
        Assert.Equal(product.Slug, otherGender.Slug);
        Assert.Equal($"/girls/dresses/{taken.Slug}", taken.Path);
    }

    [Fact]
    [Trait("Spec", "URL-7")]
    public async Task SuggestSlug_NeverReturnsAReservedWord()
    {
        using var host = new CatalogHost(postgres);

        var suggestion = await host.SendAsync(new SuggestSlugQuery("Cart", 2, 3));

        Assert.Equal("cart-2", suggestion.Slug);
    }

    [Fact]
    [Trait("Spec", "URL-6")]
    public async Task SuggestSlug_FallsBackWhenTheNameHasNoUsableLetters()
    {
        using var host = new CatalogHost(postgres);

        var suggestion = await host.SendAsync(new SuggestSlugQuery("!!!", 2, 3));

        Assert.StartsWith("product", suggestion.Slug, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "URL-1")]
    [Trait("Spec", "URL-2")]
    public async Task ChangingTheSlugOfAPublishedProduct_KeepsTheOldAddressMovingToTheNewOneInOneHop()
    {
        using var host = new CatalogHost(postgres);
        var published = await PublishedAsync(host);
        var newSlug = "renamed-" + Guid.NewGuid().ToString("N")[..8];

        var moved = await host.SendAsync(new SetProductPathCommand(published.Id, 2, 3, newSlug, published.Version, "Test Person"));

        Assert.Equal($"/girls/dresses/{newSlug}", moved.Path);
        var old = await ResolveAsync(host, published.Path);
        Assert.Equal(PathOutcome.Moved, old.Outcome);
        Assert.Equal(moved.Path, old.CurrentPath);
        Assert.Equal(published.Id, old.ProductId);
        var current = await ResolveAsync(host, moved.Path);
        Assert.Equal(PathOutcome.Current, current.Outcome);
        Assert.Equal(moved.Path, current.CurrentPath);
    }

    [Fact]
    [Trait("Spec", "URL-3")]
    public async Task SeveralSlugChanges_EveryOldAddressPointsStraightAtTheCurrentOne()
    {
        using var host = new CatalogHost(postgres);
        var published = await PublishedAsync(host);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var version = published.Version;
        var paths = new List<string> { published.Path };
        foreach (var step in new[] { "one", "two", "three" })
        {
            var result = await host.SendAsync(new SetProductPathCommand(published.Id, 2, 3, $"{step}-{tag}", version, "Test Person"));
            version = result.Version;
            paths.Add(result.Path);
        }

        foreach (var oldPath in paths.Take(3))
        {
            var resolved = await ResolveAsync(host, oldPath);
            Assert.Equal(PathOutcome.Moved, resolved.Outcome);
            Assert.Equal(paths[3], resolved.CurrentPath);
        }

        var history = await host.SendAsync(new GetPathHistoryQuery(published.Id));
        Assert.Equal(4, history.Count);
        Assert.Single(history, h => h.IsCurrent);
        Assert.Equal(paths[3], history[0].Path);
    }

    [Fact]
    [Trait("Spec", "URL-2")]
    public async Task ChangingAPublishedProductsPath_WritesOneChangedEvent_WithBothPaths()
    {
        using var host = new CatalogHost(postgres);
        var published = await PublishedAsync(host);
        var newSlug = "evented-" + Guid.NewGuid().ToString("N")[..8];

        var moved = await host.SendAsync(new SetProductPathCommand(published.Id, 2, 3, newSlug, published.Version, "Test Person"));

        await using var db = postgres.NewContext();
        var payload = await db.Set<OutboxMessage>().Where(m => m.EventType == "ProductChangedEvent" && m.AggregateId == published.Id).Select(m => m.Payload).SingleAsync();
        Assert.Contains(moved.Path, payload, StringComparison.Ordinal);
        Assert.Contains(published.Path, payload, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "URL-11")]
    public async Task AnAddressAnotherProductOnceHad_CannotBeTaken()
    {
        using var host = new CatalogHost(postgres);
        var first = await PublishedAsync(host);
        var second = await host.NewProductAsync();
        await host.SendAsync(new SetProductPathCommand(first.Id, 2, 3, "moved-" + Guid.NewGuid().ToString("N")[..8], first.Version, "Test Person"));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            host.SendAsync(new SetProductPathCommand(second.Id, 2, 3, first.Slug, second.Version, "Test Person")));

        Assert.Contains("already used", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "URL-11")]
    public async Task ASlugAnotherCurrentProductHas_CannotBeTaken_ButTheSameSlugInAnotherCategoryCan()
    {
        using var host = new CatalogHost(postgres);
        var first = await host.NewProductAsync();
        var second = await host.NewProductAsync();

        await Assert.ThrowsAsync<DomainException>(() =>
            host.SendAsync(new SetProductPathCommand(second.Id, 2, 3, first.Slug, second.Version, "Test Person")));
        var elsewhere = await host.SendAsync(new SetProductPathCommand(second.Id, 2, 1, first.Slug, second.Version, "Test Person"));

        Assert.Equal($"/girls/tops/{first.Slug}", elsewhere.Path);
    }

    [Theory]
    [InlineData("cart")]
    [InlineData("admin")]
    [InlineData("Has Spaces")]
    [InlineData("../etc")]
    [InlineData("café")]
    [Trait("Spec", "URL-7")]
    [Trait("Spec", "URL-6")]
    public async Task ABadSlug_IsRefusedWithAMessage(string slug)
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetProductPathCommand(product.Id, 2, 3, slug, product.Version, "Test Person")));
    }

    [Fact]
    [Trait("Spec", "URL-8")]
    public async Task ASlugOverTheMaximumLength_IsRefused()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        var error = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new SetProductPathCommand(product.Id, 2, 3, new string('a', 81), product.Version, "Test Person")));

        Assert.Contains("80", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "URL-9")]
    public async Task AnUnknownPath_IsNotFound_AndAnArchivedProductsPath_IsGone()
    {
        using var host = new CatalogHost(postgres);
        var published = await PublishedAsync(host);
        await host.SendAsync(new ArchiveProductCommand(published.Id, published.Version, "Test Person"));

        Assert.Equal(PathOutcome.NotFound, (await ResolveAsync(host, "/girls/dresses/never-existed-" + Guid.NewGuid().ToString("N")[..6])).Outcome);
        Assert.Equal(PathOutcome.NotFound, (await ResolveAsync(host, "/nobody/dresses/x")).Outcome);
        Assert.Equal(PathOutcome.NotFound, (await ResolveAsync(host, "/girls/dresses")).Outcome);
        Assert.Equal(PathOutcome.NotFound, (await ResolveAsync(host, "/girls/dresses/x?id=1")).Outcome);
        var gone = await ResolveAsync(host, published.Path);
        Assert.Equal(PathOutcome.Gone, gone.Outcome);
        Assert.Equal(published.Id, gone.ProductId);
    }

    [Fact]
    [Trait("Spec", "URL-9")]
    public async Task ADraftOrUnpublishedProduct_IsNotFoundToThePublic()
    {
        using var host = new CatalogHost(postgres);
        var published = await PublishedAsync(host);
        var unpublished = await host.SendAsync(new UnpublishProductCommand(published.Id, published.Version, "Test Person"));

        Assert.Equal(PathOutcome.NotFound, (await ResolveAsync(host, published.Path)).Outcome);
        var draft = await host.NewProductAsync();
        Assert.Equal(PathOutcome.NotFound, (await ResolveAsync(host, draft.Path)).Outcome);
        Assert.Equal("Unpublished", unpublished.Status);
    }

    [Fact]
    [Trait("Spec", "URL-5")]
    public async Task ThePathLookup_IsCaseAndSlashInsensitive_ButReportsTheCanonicalPath()
    {
        using var host = new CatalogHost(postgres);
        var published = await PublishedAsync(host);

        var resolved = await ResolveAsync(host, published.Path.ToUpperInvariant() + "/");

        Assert.Equal(PathOutcome.Current, resolved.Outcome);
        Assert.Equal(published.Path, resolved.CurrentPath);
    }

    [Fact]
    [Trait("Spec", "URL-9")]
    public async Task TheContractResolvesInProcess_ForTheStorefront()
    {
        using var host = new CatalogHost(postgres);
        var published = await PublishedAsync(host);

        var result = await host.InScopeAsync(sp => sp.GetRequiredService<IProductPathQueries>().ResolveAsync(published.Path, default));

        Assert.Equal(PathOutcome.Current, result.Outcome);
    }

    [Fact]
    [Trait("Spec", "NFR-01")]
    public async Task ResolvingAPath_IsOneIndexedReadOfTheHistory_PlusTheLookupFakes()
    {
        var counter = new CommandCounter();
        using var host = new CatalogHost(postgres, counter: counter);
        var published = await PublishedAsync(host);
        counter.Reset();

        await ResolveAsync(host, published.Path);

        Assert.Equal(1, counter.Count);
    }

    [Fact]
    [Trait("Spec", "URL-3")]
    public async Task AStalePathChange_IsRefused()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();
        await host.SendAsync(new SetProductPathCommand(product.Id, 2, 3, "first-" + Guid.NewGuid().ToString("N")[..8], product.Version, "Editor One"));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            host.SendAsync(new SetProductPathCommand(product.Id, 2, 3, "second-" + Guid.NewGuid().ToString("N")[..8], product.Version, "Editor Two")));
    }

    [Fact]
    public async Task ADraftsPathChange_KeepsNoHistory_BecauseItWasNeverPublic()
    {
        using var host = new CatalogHost(postgres);
        var product = await host.NewProductAsync();

        await host.SendAsync(new SetProductPathCommand(product.Id, 2, 3, "draft-" + Guid.NewGuid().ToString("N")[..8], product.Version, "Test Person"));

        Assert.Empty(await host.SendAsync(new GetPathHistoryQuery(product.Id)));
    }
}
