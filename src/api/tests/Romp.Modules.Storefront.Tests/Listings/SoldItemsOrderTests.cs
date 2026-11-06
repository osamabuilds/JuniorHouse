using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>sold-items-in-listings task 2: sold-out products stay in scope and always come after the in-stock ones (SLD-1, SLD-2). Real PostgreSQL.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SoldItemsOrderTests(StorefrontPostgres postgres)
{
    private static readonly SeedVariant[] SoldOut = [new("RED", "5-6Y", 800m, 0)];

    private static ListingFilter Filter(string? sort = null, string? page = null, string? pageSize = null) =>
        ListingFilter.Parse(null, null, null, null, sort, page, pageSize).Filter!;

    private static async Task<string[]> NamesAsync(StorefrontHost host, ListingFilter filter)
    {
        var result = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), filter));
        Assert.Equal(ListingOutcome.Found, result.Outcome);
        return [.. result.Value!.Tiles.Select(t => t.Name)];
    }

    private static async Task SeedAsync(StorefrontHost host)
    {
        var now = DateTimeOffset.UtcNow;
        // The newest product is sold out: it must still come last.
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 900m, 5)], publishedAt: now.AddDays(-3), name: "Old in stock");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", SoldOut, publishedAt: now.AddDays(-1), name: "Newest sold out");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES", [new("RED", "5-6Y", 500m, 5)], publishedAt: now.AddDays(-2), name: "Mid in stock");
        await ListingSeeder.ListableAsync(host, 4, "GIRLS", "DRESSES", SoldOut, publishedAt: now.AddDays(-4), name: "Oldest sold out");
    }

    [Fact]
    [Trait("Spec", "SLD-1")]
    [Trait("Spec", "SLD-2")]
    public async Task ASoldOutProduct_IsListed_AfterEveryInStockOne_WhateverItsAge()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await SeedAsync(host);

        Assert.Equal(["Mid in stock", "Old in stock", "Newest sold out", "Oldest sold out"], await NamesAsync(host, Filter()));
    }

    [Theory]
    [InlineData("price-asc")]
    [InlineData("price-desc")]
    [Trait("Spec", "SLD-2")]
    public async Task APriceSort_AppliesInsideEachGroup_AndSoldOutStillComesLast(string sort)
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await SeedAsync(host);

        var names = await NamesAsync(host, Filter(sort: sort));

        Assert.Equal(["Newest sold out", "Oldest sold out"], names[2..].Order());
        Assert.Equal(sort == "price-asc" ? ["Mid in stock", "Old in stock"] : ["Old in stock", "Mid in stock"], names[..2]);
    }

    [Fact]
    [Trait("Spec", "SLD-2")]
    public async Task TheGroupsHoldAcrossPages_WithNoRepeatOrGap()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await SeedAsync(host);

        var seen = new List<string>();
        for (var page = 1; page <= 2; page++)
        {
            seen.AddRange(await NamesAsync(host, Filter(page: page.ToString(System.Globalization.CultureInfo.InvariantCulture), pageSize: "2")));
        }

        Assert.Equal(["Mid in stock", "Old in stock", "Newest sold out", "Oldest sold out"], seen);
    }
}
