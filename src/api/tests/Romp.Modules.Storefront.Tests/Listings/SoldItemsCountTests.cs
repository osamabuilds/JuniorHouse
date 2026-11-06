using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>sold-items-in-listings task 6: the product count, the page count and the filter options include sold-out products (SLD-3). Real PostgreSQL.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SoldItemsCountTests(StorefrontPostgres postgres)
{
    private static ListingFilter Filter(string? pageSize = null, string? stock = null) =>
        ListingFilter.Parse(null, null, null, null, null, null, pageSize, stock).Filter!;

    private static async Task SeedAsync(StorefrontHost host)
    {
        // Three in stock, two sold out: five products in scope.
        for (var id = 1; id <= 3; id++)
        {
            await ListingSeeder.ListableAsync(host, id, "GIRLS", "DRESSES", [new("RED", "5-6Y", 500m + id, 4)], name: $"In stock {id}");
        }

        await ListingSeeder.ListableAsync(host, 4, "GIRLS", "DRESSES", [new("NAVY", "9-10Y", 2000m, 0)], name: "Sold out 4");
        await ListingSeeder.ListableAsync(host, 5, "GIRLS", "DRESSES", [new("NAVY", "9-10Y", 2100m, 0)], name: "Sold out 5");
    }

    [Fact]
    [Trait("Spec", "SLD-3")]
    public async Task TheTotalAndThePageCount_IncludeSoldOutProducts()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await SeedAsync(host);

        var result = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), Filter(pageSize: "2")));

        var page = result.Value!;
        Assert.Equal((5, 3, 2), (page.Total, page.TotalPages, page.Tiles.Count));
    }

    [Fact]
    [Trait("Spec", "SLD-3")]
    [Trait("Spec", "SLD-4")]
    public async Task InStockOnly_TheCountsFollow_AndLeaveTheSoldOutOnesOut()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await SeedAsync(host);

        var result = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), Filter(pageSize: "2", stock: "in")));

        var page = result.Value!;
        Assert.Equal((3, 2), (page.Total, page.TotalPages));
    }

    [Fact]
    [Trait("Spec", "SLD-3")]
    public async Task TheFilterOptions_IncludeTheSizesColoursAndPricesOfSoldOutProducts()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await SeedAsync(host);

        var options = (await host.SendAsync(new GetListingOptionsQuery(ListingScope.Audience("girls")))).Value!;

        Assert.Equal(["5-6y", "9-10y"], options.Sizes.Select(o => o.Value).ToArray());
        Assert.Equal(["navy", "red"], options.Colours.Select(o => o.Value).ToArray());
        Assert.Equal((501m, 2100m), (options.PriceMin, options.PriceMax));
    }
}
