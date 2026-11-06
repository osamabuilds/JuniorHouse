using System.Text.Json;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>sold-items-in-listings task 5: a tile says whether the whole product is sold out, and never how many are left (SLD-5, PCT-3). Real PostgreSQL.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class ListingTileSoldOutTests(StorefrontPostgres postgres)
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static async Task<ListingPageDto> PageAsync(StorefrontHost host)
    {
        var filter = ListingFilter.Parse(null, null, null, null, null, null, null).Filter!;
        var result = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), filter));
        Assert.Equal(ListingOutcome.Found, result.Outcome);
        return result.Value!;
    }

    [Fact]
    [Trait("Spec", "SLD-5")]
    public async Task ATile_IsSoldOut_OnlyWhenNothingOfTheProductCanBeBought()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 5)], name: "In stock");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 0), new("RED", "7-8Y", 800m, 2)], name: "Some sizes out");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 0), new("NAVY", "5-6Y", 800m, 0)], name: "All out");

        var tiles = (await PageAsync(host)).Tiles.ToDictionary(t => t.Name);

        Assert.False(tiles["In stock"].SoldOut);
        Assert.False(tiles["Some sizes out"].SoldOut);
        Assert.True(tiles["Some sizes out"].HasSoldOutSizes);
        Assert.True(tiles["All out"].SoldOut);
        Assert.All(tiles["All out"].Sizes, s => Assert.True(s.SoldOut));
    }

    [Fact]
    [Trait("Spec", "SLD-5")]
    [Trait("Spec", "PCT-3")]
    public async Task ASoldOutTile_StillCarriesItsPriceAndPath_AndTheJsonHasNoQuantity()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 0)], name: "All out");

        var page = await PageAsync(host);
        var tile = Assert.Single(page.Tiles);

        Assert.Equal(800m, tile.Price);
        Assert.False(string.IsNullOrEmpty(tile.Path));

        var json = JsonSerializer.Serialize(page, WebJson);
        Assert.Contains("\"soldOut\":true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("quantit", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("available", json.Replace("\"availability\"", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
    }
}
