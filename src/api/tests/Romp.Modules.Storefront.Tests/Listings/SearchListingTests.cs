using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>Listings tasks 3 to 5: <see cref="SearchListingQuery"/> and its companions against a real PostgreSQL (LST-1..9, 14..16).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SearchListingTests(StorefrontPostgres postgres)
{
    private static ListingFilter Filter(string? size = null, string? colour = null, string? age = null, string? price = null, string? sort = null, string? page = null, string? pageSize = null) =>
        ListingFilter.Parse(size is null ? null : [size], colour is null ? null : [colour], age, price, sort, page, pageSize).Filter!;

    private static async Task<ListingPageDto> PageAsync(StorefrontHost host, ListingScope scope, ListingFilter? filter = null)
    {
        var result = await host.SendAsync(new SearchListingQuery(scope, filter ?? Filter()));
        Assert.Equal(ListingOutcome.Found, result.Outcome);
        return result.Value!;
    }

    private static string[] Names(ListingPageDto page) => [.. page.Tiles.Select(t => t.Name)];

    [Fact]
    [Trait("Spec", "LST-9")]
    public async Task AnAudience_ListsItsListableProducts_AndACategoryOnlyItsOwn()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", name: "Dress");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "TOPS", name: "Top");
        await ListingSeeder.ListableAsync(host, 3, "BOYS", "TOPS", name: "Boys top");

        var girls = await PageAsync(host, ListingScope.Audience("girls"));
        var dresses = await PageAsync(host, ListingScope.InCategory("girls", "dresses"));

        Assert.Equal(["Top", "Dress"], Names(girls)); // newest id first: equal publish dates
        Assert.Equal(["Dress"], Names(dresses));
        Assert.Equal(("Girls", "Dresses"), (dresses.AudienceName, dresses.CategoryName));
        Assert.Equal(["Home", "Girls", "Dresses"], dresses.Breadcrumbs.Select(b => b.Label).ToArray());
    }

    [Fact]
    [Trait("Spec", "LST-15")]
    public async Task AProductWithNothingInStock_IsNeverListed_ButOneWithSomeSizesSoldOutIs()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 0)], name: "Gone");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 4), new("RED", "7-8Y", 800m, 0)], name: "Partly");

        var page = await PageAsync(host, ListingScope.Audience("girls"));

        var tile = Assert.Single(page.Tiles);
        Assert.Equal("Partly", tile.Name);
        Assert.True(tile.HasSoldOutSizes);
    }

    [Fact]
    [Trait("Spec", "LST-1")]
    public async Task ASizeFilter_ShowsOnlyProductsWithThatSizeInStock()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 3)], name: "Has 5-6");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [new("RED", "7-8Y", 800m, 3)], name: "Has 7-8");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 0), new("RED", "7-8Y", 800m, 3)], name: "5-6 sold out");

        Assert.Equal(["Has 5-6"], Names(await PageAsync(host, ListingScope.Audience("girls"), Filter(size: "5-6y"))));
    }

    [Fact]
    [Trait("Spec", "LST-2")]
    public async Task SizeColourAndPrice_MustAllHoldOnTheSameVariant()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        // Navy exists only in 7-8Y; 5-6Y exists only in red: "navy in 5-6Y" must not match this product.
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 3), new("NAVY", "7-8Y", 800m, 3)], name: "Split");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [new("NAVY", "5-6Y", 800m, 3)], name: "Match");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES", [new("NAVY", "5-6Y", 5000m, 3)], name: "Too dear");

        var page = await PageAsync(host, ListingScope.Audience("girls"), Filter(size: "5-6y", colour: "navy", price: "100-1000"));

        Assert.Equal(["Match"], Names(page));
    }

    [Fact]
    [Trait("Spec", "LST-16")]
    public async Task AnAgeFilter_CombinesByAnd_AndAnUnknownAgeMatchesNothingWithoutAnError()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", age: "AGE_3Y", name: "Three");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", age: "KIDS", name: "Kids");

        Assert.Equal(["Three"], Names(await PageAsync(host, ListingScope.Audience("girls"), Filter(age: "age-3y"))));
        Assert.Empty((await PageAsync(host, ListingScope.Audience("girls"), Filter(age: "nonsense"))).Tiles);
    }

    [Fact]
    [Trait("Spec", "LST-4")]
    public async Task APriceSort_KeepsItsOrderAcrossPages_WithNoRepeatOrGap()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        foreach (var id in Enumerable.Range(1, 7))
        {
            // Prices repeat (700, 700, 800, 800, ...) so only the id tie-break keeps pages stable.
            await ListingSeeder.ListableAsync(host, id, "GIRLS", "DRESSES", [new("RED", "5-6Y", 700m + (id / 2 * 100m), 5)], name: $"P{id}");
        }

        var seen = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            seen.AddRange(Names(await PageAsync(host, ListingScope.Audience("girls"), Filter(sort: "price-asc", page: page.ToString(System.Globalization.CultureInfo.InvariantCulture), pageSize: "3"))));
        }

        Assert.Equal(7, seen.Distinct().Count());
        var prices = (await PageAsync(host, ListingScope.Audience("girls"), Filter(sort: "price-asc", pageSize: "48"))).Tiles.Select(t => t.Price).ToArray();
        Assert.Equal(prices.Order(), prices);
        Assert.Equal(seen, (await PageAsync(host, ListingScope.Audience("girls"), Filter(sort: "price-asc", pageSize: "48"))).Tiles.Select(t => t.Name));
    }

    [Fact]
    [Trait("Spec", "LST-4")]
    public async Task APriceSort_Descending_PutsTheDearestFirst()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 500m, 5)], name: "Cheap");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [new("RED", "5-6Y", 900m, 5)], name: "Dear");

        Assert.Equal(["Dear", "Cheap"], Names(await PageAsync(host, ListingScope.Audience("girls"), Filter(sort: "price-desc"))));
    }

    [Fact]
    [Trait("Spec", "LST-7")]
    public async Task NewIn_ListsOnlyProductsPublishedInTheWindow_NewestFirst()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-2), name: "Recent");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-1), name: "Newest");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-90), name: "Old");
        await ListingSeeder.ListableAsync(host, 4, "BOYS", "TOPS", publishedAt: DateTimeOffset.UtcNow.AddDays(-1), name: "Boys new");

        Assert.Equal(["Newest", "Recent"], Names(await PageAsync(host, ListingScope.NewIn("girls"))));
        Assert.Equal(["Boys new", "Newest", "Recent"], Names(await PageAsync(host, ListingScope.NewIn(null))));
    }

    [Fact]
    [Trait("Spec", "LST-8")]
    public async Task Sale_ListsProductsWithACompareAtAbovePrice_AndShowsTheSaving()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 5, CompareAt: 1000m)], name: "On sale");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 5)], name: "Full price");

        var page = await PageAsync(host, ListingScope.Sale("girls"));

        var tile = Assert.Single(page.Tiles);
        Assert.Equal(("On sale", 800m, 1000m, 20), (tile.Name, tile.Price, tile.CompareAtPrice, tile.SavingPercent));
        Assert.Equal(20, page.MaxSavingPercent);
        Assert.Equal(["Home", "Girls", "Sale"], page.Breadcrumbs.Select(b => b.Label).ToArray());
    }

    [Fact]
    [Trait("Spec", "LST-9")]
    public async Task ACollection_ListsItsProducts_WithTheCollectionNameAsTheTitle()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", collection: "SUMMER", name: "In");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", name: "Out");

        var page = await PageAsync(host, ListingScope.OfCollection("summer"));

        Assert.Equal(["In"], Names(page));
        Assert.Equal("Summer", page.Title);
    }

    [Fact]
    [Trait("Spec", "LST-5")]
    public async Task ACategoryThatExistsButHasNothingInStock_IsAnEmptyPage_NotANotFound()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES");

        var page = await PageAsync(host, ListingScope.InCategory("girls", "empty"));

        Assert.Empty(page.Tiles);
        Assert.Equal((0, 1), (page.Total, page.TotalPages));
    }

    [Fact]
    [Trait("Spec", "LST-11")]
    public async Task AnUnknownGenderCategoryOrCollection_IsNotFound()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);

        foreach (var scope in new[] { ListingScope.Audience("martians"), ListingScope.InCategory("girls", "nope"), ListingScope.OfCollection("nope"), ListingScope.NewIn("nope") })
        {
            Assert.Equal(ListingOutcome.ScopeNotFound, (await host.SendAsync(new SearchListingQuery(scope, Filter()))).Outcome);
        }
    }

    [Fact]
    [Trait("Spec", "LST-14")]
    public async Task APagePastTheEnd_IsNotFound_AndAHugePageNumberDoesNotScan()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES");

        Assert.Equal(ListingOutcome.PastTheEnd, (await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), Filter(page: "2")))).Outcome);
        Assert.Equal(ListingOutcome.PastTheEnd, (await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), Filter(page: "999999999")))).Outcome);
    }

    [Fact]
    [Trait("Spec", "LST-12")]
    public async Task ATile_CarriesItsMainImageDerivatives_AndNoStockCount()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 7), new("NAVY", "5-6Y", 800m, 2)]);

        var tile = Assert.Single((await PageAsync(host, ListingScope.Audience("girls"))).Tiles);

        Assert.Equal(2, tile.ColourCount);
        Assert.NotNull(tile.Image);
        Assert.Equal([320, 640], tile.Image.Sources.Select(s => s.Width).ToArray());
        Assert.All(tile.Image.Sources, s => Assert.StartsWith("/media/", s.Webp, StringComparison.Ordinal));
        Assert.All(tile.Image.Sources, s => Assert.StartsWith("/media/", s.Jpeg, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "LST-13")]
    public async Task APage_UsesAConstantNumberOfQueries_HoweverManyProducts()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        var counter = new QueryCounter();
        await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var handler = new SearchListingQueryHandler(context, host.Lookups, new Romp.Modules.Storefront.Infrastructure.CatalogLookupCache(TimeProvider.System), new FakeListingSettings(), TimeProvider.System);

        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES");
        await handler.Handle(new SearchListingQuery(ListingScope.Audience("girls"), Filter()), default);
        var forOne = counter.Count;

        foreach (var id in Enumerable.Range(2, 12))
        {
            await ListingSeeder.ListableAsync(host, id, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 3), new("NAVY", "7-8Y", 900m, 3)]);
        }

        counter.Reset();
        var page = await handler.Handle(new SearchListingQuery(ListingScope.Audience("girls"), Filter(size: "5-6y", sort: "price-asc")), default);

        Assert.Equal(13, page.Value!.Tiles.Count);
        Assert.Equal(forOne, counter.Count);
        Assert.Equal(4, counter.Count);
    }

    [Fact]
    [Trait("Spec", "LST-1")]
    public async Task TheOptions_ListOnlyValuesPresentInTheScope()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "5-6Y", 800m, 3), new("NAVY", "7-8Y", 1200m, 0)], age: "AGE_3Y");
        await ListingSeeder.ListableAsync(host, 2, "BOYS", "TOPS", [new("GREEN", "7-8Y", 300m, 3)]);

        var result = await host.SendAsync(new GetListingOptionsQuery(ListingScope.Audience("girls")));

        var options = result.Value!;
        Assert.Equal(["5-6y"], options.Sizes.Select(o => o.Value).ToArray()); // 7-8Y is sold out
        Assert.Equal(["red"], options.Colours.Select(o => o.Value).ToArray());
        Assert.Equal((800m, 800m), (options.PriceMin, options.PriceMax));
        Assert.Equal(["age-3y"], options.Ages.Select(o => o.Value).ToArray());
    }

    [Fact]
    [Trait("Spec", "LST-5")]
    [Trait("Spec", "LST-6")]
    public async Task TheEmptyState_OffersNewInAndTheAudiencesOtherCategories()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "TOPS");

        var result = await host.SendAsync(new GetEmptyStateQuery(ListingScope.InCategory("girls", "dresses")));

        Assert.Equal([("New in", "/girls/new-in"), ("Tops", "/girls/tops")], result.Value!.Suggestions.Select(s => (s.Label, s.Path)).ToArray());
    }
}
