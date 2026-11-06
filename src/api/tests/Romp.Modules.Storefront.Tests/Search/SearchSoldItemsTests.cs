using Romp.Modules.Storefront.Application.Search;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Search;

/// <summary>sold-items-in-listings task 8: search finds sold-out products but lists them after the in-stock ones; suggestions stay in stock only (SLD-7). Real PostgreSQL.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SearchSoldItemsTests(StorefrontPostgres postgres)
{
    private static readonly SeedVariant InStock = new("RED", "5-6Y", 800m, 10);
    private static readonly SeedVariant SoldOut = new("RED", "5-6Y", 100m, 0);

    private static ListingFilter Filter(string? sort = null, string? stock = null) =>
        ListingFilter.Parse(null, null, null, null, sort, null, null, stock).Filter!;

    private static async Task<string[]> NamesAsync(StorefrontHost host, string text, ListingFilter filter)
    {
        var result = await host.SendAsync(new SearchProductsQuery(text, filter));
        return [.. result.Page.Tiles.Select(t => t.Name)];
    }

    private static async Task<StorefrontHost> SeedAsync(StorefrontPostgres postgres)
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        // The sold-out frock is the best match by relevance and the cheapest, yet it must still come last.
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [SoldOut], name: "Frock frock frock");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [InStock], name: "Ready frock");
        return host;
    }

    [Fact]
    [Trait("Spec", "SLD-7")]
    public async Task SoldOutMatches_AreListedAfterInStockOnes_WhateverTheRelevanceOrPriceSort()
    {
        using var host = await SeedAsync(postgres);

        Assert.Equal(["Ready frock", "Frock frock frock"], await NamesAsync(host, "frock", Filter()));
        Assert.Equal(["Ready frock", "Frock frock frock"], await NamesAsync(host, "frock", Filter(sort: "price-asc")));
        Assert.Equal(["Ready frock", "Frock frock frock"], await NamesAsync(host, "frock", Filter(sort: "price-desc")));
    }

    [Fact]
    [Trait("Spec", "SLD-7")]
    public async Task SoldOutMatches_AreCounted_UnlessInStockOnlyIsOn()
    {
        using var host = await SeedAsync(postgres);

        var all = await host.SendAsync(new SearchProductsQuery("frock", Filter()));
        var inStock = await host.SendAsync(new SearchProductsQuery("frock", Filter(stock: "in")));

        Assert.Equal((2, 1), (all.Page.Total, inStock.Page.Total));
        Assert.True(all.Page.Tiles[1].SoldOut);
    }

    [Fact]
    [Trait("Spec", "SLD-7")]
    public async Task Suggestions_StillOfferInStockProductsOnly()
    {
        using var host = await SeedAsync(postgres);

        var result = await host.SendAsync(new SuggestQuery("frock"));

        Assert.Equal(["Ready frock"], result.Items.Where(i => i.Kind == "product").Select(i => i.Label).ToArray());
    }

    [Fact]
    [Trait("Spec", "SLD-7")]
    public async Task ACategoryOnlySoldOutProductsLiveIn_IsNotSuggested()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "TOPS", [SoldOut], name: "Gone");

        var result = await host.SendAsync(new SuggestQuery("top"));

        Assert.DoesNotContain(result.Items, i => i.Kind == "category");
    }
}
