using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>
/// Storefront-settings ST-5 on the API side: a navigation entry that is enabled but has no live products is an empty page (found, nothing
/// listed, a normal page count), never a not-found. The page's <c>noindex</c> is asserted in the browser test LST-5.
/// </summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class StorefrontEntryEmptyStateTests(StorefrontPostgres postgres)
{
    private static ListingFilter Filter() => ListingFilter.Parse(null, null, null, null, null, null, null).Filter!;

    [Fact]
    [Trait("Spec", "ST-5")]
    public async Task AnEnabledCollectionWithNoLiveProducts_IsFound_AndEmpty()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        host.Lookups.Collections.Add(new(2, "WINTER", "Winter", 5, true));
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", collection: "SUMMER");

        var result = await host.SendAsync(new SearchListingQuery(ListingScope.OfCollection("winter"), Filter()));

        Assert.Equal(ListingOutcome.Found, result.Outcome);
        Assert.Empty(result.Value!.Tiles);
        Assert.Equal((0, 1), (result.Value.Total, result.Value.TotalPages));
        Assert.Equal("Winter", result.Value.Title);
    }

    [Fact]
    [Trait("Spec", "ST-5")]
    [Trait("Spec", "SLD-9")]
    public async Task AnEnabledAudienceWhoseProductsAreAllOutOfStock_IsNotEmpty_TheSoldOutPiecesShow()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 0)]);

        var result = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), Filter()));

        Assert.Equal(ListingOutcome.Found, result.Outcome);
        Assert.Single(result.Value!.Tiles);
        Assert.True(result.Value.Tiles[0].SoldOut);
    }

    [Fact]
    [Trait("Spec", "SLD-9")]
    public async Task AnAudienceWhoseProductsAreAllOutOfStock_IsEmpty_OnlyWhenInStockOnlyIsOn()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 0)]);
        var inStock = ListingFilter.Parse(null, null, null, null, null, null, null, "in").Filter!;

        var result = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), inStock));

        Assert.Equal(ListingOutcome.Found, result.Outcome);
        Assert.Empty(result.Value!.Tiles);
    }
}
