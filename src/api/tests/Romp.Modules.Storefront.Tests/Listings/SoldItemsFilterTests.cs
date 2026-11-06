using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>sold-items-in-listings task 4: size, colour and price match any variant, sold out or not, unless "in stock only" is on (SLD-4, SLD-8). Real PostgreSQL.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SoldItemsFilterTests(StorefrontPostgres postgres)
{
    private static ListingFilter Filter(string? size = null, string? price = null, string? stock = null) =>
        ListingFilter.Parse(size is null ? null : [size], null, null, price, null, null, null, stock).Filter!;

    private static async Task<string[]> NamesAsync(StorefrontHost host, ListingFilter filter)
    {
        var result = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), filter));
        Assert.Equal(ListingOutcome.Found, result.Outcome);
        return [.. result.Value!.Tiles.Select(t => t.Name)];
    }

    private static async Task SeedAsync(StorefrontHost host)
    {
        // "Mixed": 5-6Y sold out, 7-8Y in stock. "Gone": every size sold out. "Plenty": 5-6Y in stock.
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 500m, 0), new("RED", "7-8Y", 900m, 3)], name: "Mixed");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [new("RED", "5-6Y", 500m, 0)], name: "Gone");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES", [new("RED", "5-6Y", 500m, 4)], name: "Plenty");
    }

    [Fact]
    [Trait("Spec", "SLD-8")]
    public async Task ASizeFilter_MatchesSoldOutVariants_SoSoldOutProductsStayListed()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await SeedAsync(host);

        var names = await NamesAsync(host, Filter(size: "5-6y"));

        // "Mixed" and "Plenty" are in stock (newest first: Plenty), "Gone" is sold out and last.
        Assert.Equal(["Plenty", "Mixed", "Gone"], names);
    }

    [Fact]
    [Trait("Spec", "SLD-8")]
    public async Task APriceFilter_MatchesASoldOutVariantsPrice()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await SeedAsync(host);

        var names = await NamesAsync(host, Filter(price: "400-600"));

        Assert.Equal(["Gone", "Mixed", "Plenty"], names.Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Spec", "SLD-4")]
    [Trait("Spec", "SLD-8")]
    public async Task InStockOnly_LeavesOutSoldOutProducts_AndOnlyMatchesInStockVariants()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await SeedAsync(host);

        // No other filter: products with something in stock.
        Assert.Equal(["Mixed", "Plenty"], (await NamesAsync(host, Filter(stock: "in"))).Order(StringComparer.Ordinal));

        // With a size: "Mixed" has 5-6Y, but sold out, so it drops out; only the in-stock 5-6Y remains.
        Assert.Equal(["Plenty"], await NamesAsync(host, Filter(size: "5-6y", stock: "in")));
    }
}
