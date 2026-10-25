using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Search;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Search;

/// <summary>Search task 4: <see cref="SearchProductsQuery"/> against a real PostgreSQL (SRC-1, 2, 3, 8, 12, 14).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SearchProductsTests(StorefrontPostgres postgres)
{
    private static readonly SeedVariant InStock = new("RED", "5-6Y", 800m, 10);

    private static ListingFilter Filter(string? size = null, string? sort = null, string? page = null, string? pageSize = null) =>
        ListingFilter.Parse(size is null ? null : [size], null, null, null, sort, page, pageSize).Filter!;

    private static async Task<SearchProductsResult> SearchAsync(StorefrontHost host, string? text, ListingFilter? filter = null) =>
        await host.SendAsync(new SearchProductsQuery(text, filter ?? Filter()));

    private static string[] Names(SearchProductsResult result) => [.. result.Page.Tiles.Select(t => t.Name)];

    /// <summary>Products named "Plain ..." sit in Tops, the rest in Dresses: a category word is searchable, so a distractor must not share it.</summary>
    private async Task<StorefrontHost> HostAsync(params (long Id, string Name)[] products)
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        foreach (var (id, name) in products)
        {
            await ListingSeeder.ListableAsync(host, id, "GIRLS", name.StartsWith("Plain", StringComparison.Ordinal) ? "TOPS" : "DRESSES", [InStock], name: name);
        }

        return host;
    }


    [Fact]
    [Trait("Spec", "SRC-1")]
    public async Task Search_Keyword_FindsMatchingProductsOnly()
    {
        using var host = await HostAsync((1, "Denim dungaree"), (2, "Floral frock"), (3, "Plain tee"));

        var result = await SearchAsync(host, "dungaree");

        Assert.Equal(SearchOutcome.Found, result.Outcome);
        Assert.Equal(["Denim dungaree"], Names(result));
        Assert.Equal("dungaree", result.Page.Query);
        Assert.Equal(1, result.Page.Total);
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public async Task Search_Synonym_FindsTheSameProductsForEitherTerm()
    {
        // The seeded description of every test product is "A dress.", so the dress group is not used here.
        using var host = await HostAsync((1, "Cotton kameez"), (2, "Plain tee"));

        Assert.Equal(["Cotton kameez"], Names(await SearchAsync(host, "kurta")));
        Assert.Equal(["Cotton kameez"], Names(await SearchAsync(host, "kurtha")));
        Assert.Equal(["Cotton kameez"], Names(await SearchAsync(host, "kameez")));
    }

    [Fact]
    [Trait("Spec", "SRC-8")]
    public async Task Search_OneSlip_StillFindsTheProduct()
    {
        using var host = await HostAsync((1, "Denim jumper"), (2, "Plain tee"));

        var result = await SearchAsync(host, "jumpr");

        Assert.Equal(["Denim jumper"], Names(result));
    }

    [Theory]
    [Trait("Spec", "SRC-8")]
    [InlineData("summer")]
    [InlineData("dresses")]
    [InlineData("navy")]
    [InlineData("girls")]
    public async Task Search_CategoryCollectionColourAndAudience_Match(string word)
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        using var _ = host;
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new SeedVariant("NAVY", "5-6Y", 800m, 10)], collection: "SUMMER", name: "Alpha");
        await ListingSeeder.ListableAsync(host, 2, "BOYS", "TOPS", [InStock], name: "Beta");

        Assert.Equal(["Alpha"], Names(await SearchAsync(host, word)));
    }

    [Fact]
    [Trait("Spec", "SRC-8")]
    public async Task Search_NameMatch_RanksAboveAWordFoundOnlyInTheCollection()
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        using var _ = host;
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [InStock], collection: "SUMMER", name: "Alpha");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [InStock], name: "Summer top");

        Assert.Equal(["Summer top", "Alpha"], Names(await SearchAsync(host, "summer")));
    }

    [Theory]
    [Trait("Spec", "SRC-2")]
    [InlineData("qzxjvk")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Search_NothingMatches_IsANormalEmptyAnswer(string? text)
    {
        using var host = await HostAsync((1, "Floral frock"));

        var result = await SearchAsync(host, text);

        Assert.Equal(SearchOutcome.NoResults, result.Outcome);
        Assert.Empty(result.Page.Tiles);
        Assert.Equal(0, result.Page.Total);
    }

    [Theory]
    [Trait("Spec", "SRC-3")]
    [InlineData("' OR 1=1 --")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("& | ! ( ) :*")]
    [InlineData("'; DROP TABLE \"STFR\".\"PRDT_MAIN\"; --")]
    [InlineData("\0‮​")]
    [InlineData("😀😀😀")]
    [InlineData("a\uD800b")]
    public async Task Search_HostileText_IsPlainTextAndNeverAnError(string text)
    {
        using var host = await HostAsync((1, "Floral frock"));

        var result = await SearchAsync(host, text);

        Assert.NotNull(result.Page);
        Assert.True(result.Page.Query.Length <= SearchQueryLimit);
        Assert.Equal(1, await host.QueryAsync(async db => await db.Products.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "SRC-3")]
    public async Task Search_VeryLongText_IsCutAndAnswered()
    {
        using var host = await HostAsync((1, "Floral frock"));

        var result = await SearchAsync(host, new string('a', 100_000));

        Assert.True(result.Page.Query.Length <= SearchQueryLimit);
        Assert.Equal(SearchOutcome.NoResults, result.Outcome);
    }

    private const int SearchQueryLimit = 100;

    [Fact]
    [Trait("Spec", "SRC-14")]
    public async Task Search_ProductWithNothingInStock_IsNotReturned()
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        using var _ = host;
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new SeedVariant("RED", "5-6Y", 800m, 0)], name: "Sold out frock");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [InStock], name: "Ready frock");

        Assert.Equal(["Ready frock"], Names(await SearchAsync(host, "frock")));
    }

    [Fact]
    [Trait("Spec", "SRC-1")]
    public async Task Search_FiltersAndSortApplyToTheMatches()
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        using var _ = host;
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new SeedVariant("RED", "5-6Y", 900m, 5)], name: "Frock one");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", [new SeedVariant("RED", "3-4Y", 500m, 5)], name: "Frock two");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES", [new SeedVariant("RED", "5-6Y", 700m, 5)], name: "Frock three");

        Assert.Equal(["Frock two", "Frock three", "Frock one"], Names(await SearchAsync(host, "frock", Filter(sort: "price-asc"))));
        Assert.Equal(["Frock one", "Frock three"], Names(await SearchAsync(host, "frock", Filter(size: "5-6y", sort: "price-desc"))));
    }

    [Fact]
    [Trait("Spec", "SRC-1")]
    public async Task Search_ReturnsTheFilterChoicesAmongTheMatches()
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        using var _ = host;
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new SeedVariant("RED", "5-6Y", 900m, 5)], name: "Cotton kameez");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "TOPS", [new SeedVariant("NAVY", "3-4Y", 500m, 5)], name: "Plain tee");

        var options = (await SearchAsync(host, "kurta")).Page.Options;

        Assert.Equal(["5-6y"], options.Sizes.Select(s => s.Value));
        Assert.Equal(["red"], options.Colours.Select(c => c.Value));
        Assert.Equal((900m, 900m), (options.PriceMin, options.PriceMax));
    }

    [Fact]
    [Trait("Spec", "SRC-1")]
    public async Task Search_Paging_SplitsTheMatchesAndRefusesAPagePastTheEnd()
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        using var _ = host;
        for (var i = 1; i <= 5; i++)
        {
            await ListingSeeder.ListableAsync(host, i, "GIRLS", "DRESSES", [InStock], name: $"Frock {i}");
        }

        var first = await SearchAsync(host, "frock", Filter(pageSize: "2"));
        var third = await SearchAsync(host, "frock", Filter(page: "3", pageSize: "2"));
        var past = await SearchAsync(host, "frock", Filter(page: "4", pageSize: "2"));

        Assert.Equal((2, 5, 3), (first.Page.Tiles.Count, first.Page.Total, first.Page.TotalPages));
        Assert.Single(third.Page.Tiles);
        Assert.Equal(SearchOutcome.PastTheEnd, past.Outcome);
        Assert.Equal(5, first.Page.Tiles.Concat(third.Page.Tiles).Select(t => t.Path).Union((await SearchAsync(host, "frock", Filter(page: "2", pageSize: "2"))).Page.Tiles.Select(t => t.Path)).Count());
    }
}
