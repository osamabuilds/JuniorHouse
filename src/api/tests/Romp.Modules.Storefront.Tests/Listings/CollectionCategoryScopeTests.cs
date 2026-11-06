using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>sold-items-in-listings task 9: the scope "collection + category" (<c>/collections/summer/dresses</c>), any audience, sold-out pieces included (SLD-10). Real PostgreSQL.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class CollectionCategoryScopeTests(StorefrontPostgres postgres)
{
    private static ListingFilter Filter(string? stock = null) => ListingFilter.Parse(null, null, null, null, null, null, null, stock).Filter!;

    private static async Task<StorefrontHost> SeedAsync(StorefrontPostgres postgres)
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        host.Lookups.Collections.Add(new(2, "WINTER", "Winter", 20, true));
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 5)], collection: "SUMMER", name: "Girls summer dress");
        await ListingSeeder.ListableAsync(host, 2, "BOYS", "DRESSES", [new("RED", "5-6Y", 800m, 0)], collection: "SUMMER", name: "Boys summer dress, sold out");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "TOPS", [new("RED", "5-6Y", 800m, 5)], collection: "SUMMER", name: "Summer top");
        await ListingSeeder.ListableAsync(host, 4, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 5)], collection: "WINTER", name: "Winter dress");
        return host;
    }

    [Fact]
    [Trait("Spec", "SLD-10")]
    public async Task TheScope_HoldsTheCollectionsProductsOfThatCategory_ForEveryAudience_SoldOutLast()
    {
        using var host = await SeedAsync(postgres);

        var result = await host.SendAsync(new SearchListingQuery(ListingScope.OfCollectionCategory("summer", "dresses"), Filter()));

        Assert.Equal(ListingOutcome.Found, result.Outcome);
        Assert.Equal(["Girls summer dress", "Boys summer dress, sold out"], result.Value!.Tiles.Select(t => t.Name).ToArray());
        Assert.Equal("Summer Dresses", result.Value.Title);
    }

    [Fact]
    [Trait("Spec", "SLD-10")]
    public async Task TheBreadcrumb_HasFourLevels_HomeCollectionsTheCollectionAndTheCategory()
    {
        using var host = await SeedAsync(postgres);

        var page = (await host.SendAsync(new SearchListingQuery(ListingScope.OfCollectionCategory("summer", "dresses"), Filter()))).Value!;

        Assert.Equal(
            [("Home", "/"), ("Collections", "/collections"), ("Summer", "/collections/summer"), ("Dresses", null)],
            page.Breadcrumbs.Select(b => (b.Label, b.Path)).ToArray());
    }

    [Fact]
    [Trait("Spec", "SLD-10")]
    public async Task InStockOnly_AppliesToTheScopeToo_AndOptionsAndEmptyStateResolveIt()
    {
        using var host = await SeedAsync(postgres);
        var scope = ListingScope.OfCollectionCategory("summer", "dresses");

        var inStock = (await host.SendAsync(new SearchListingQuery(scope, Filter("in")))).Value!;
        var options = await host.SendAsync(new GetListingOptionsQuery(scope));
        var empty = await host.SendAsync(new GetEmptyStateQuery(scope));

        Assert.Equal(["Girls summer dress"], inStock.Tiles.Select(t => t.Name).ToArray());
        Assert.Equal(ListingOutcome.Found, options.Outcome);
        Assert.Equal(ListingOutcome.Found, empty.Outcome);
    }

    [Fact]
    [Trait("Spec", "SLD-10")]
    public async Task ACollectionOrCategoryThatDoesNotExist_IsNotFound_ButOneWithNoProductsIsAnEmptyPage()
    {
        using var host = await SeedAsync(postgres);

        foreach (var scope in new[] { ListingScope.OfCollectionCategory("nope", "dresses"), ListingScope.OfCollectionCategory("summer", "nope") })
        {
            Assert.Equal(ListingOutcome.ScopeNotFound, (await host.SendAsync(new SearchListingQuery(scope, Filter()))).Outcome);
        }

        var none = await host.SendAsync(new SearchListingQuery(ListingScope.OfCollectionCategory("winter", "tops"), Filter()));
        Assert.Equal(ListingOutcome.Found, none.Outcome);
        Assert.Empty(none.Value!.Tiles);
    }
}
