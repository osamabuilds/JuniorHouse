using Romp.Modules.Storefront.Application.Search;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Search;

/// <summary>Search task 6: <see cref="SuggestQuery"/> against a real PostgreSQL (SRC-1, 7, 14).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SuggestTests(StorefrontPostgres postgres)
{
    private static readonly SeedVariant InStock = new("RED", "5-6Y", 800m, 10);

    private static async Task<SuggestResultDto> SuggestAsync(StorefrontHost host, string? text) => await host.SendAsync(new SuggestQuery(text));

    [Fact]
    [Trait("Spec", "SRC-1")]
    public async Task Suggest_Prefix_ListsMatchingProductsWithPathAndImage()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "TOPS", [InStock], name: "Denim dungaree");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "TOPS", [InStock], name: "Plain tee");

        var result = await SuggestAsync(host, "dung");

        var item = Assert.Single(result.Items);
        Assert.Equal(("product", "Denim dungaree", "/girls/tops/p-1"), (item.Kind, item.Label, item.Path));
        Assert.NotNull(item.Image);
        Assert.Equal(800m, item.Price);
    }

    [Fact]
    [Trait("Spec", "SRC-1")]
    public async Task Suggest_CategoryName_OffersTheCategoryPageOfEachAudienceThatHasStock()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [InStock], name: "Alpha");
        await ListingSeeder.ListableAsync(host, 2, "BOYS", "TOPS", [InStock], name: "Beta");

        var result = await SuggestAsync(host, "dre");

        Assert.Contains(result.Items, i => i.Kind == "category" && i.Path == "/girls/dresses");
        Assert.DoesNotContain(result.Items, i => i.Kind == "category" && i.Path.StartsWith("/boys", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "SRC-1")]
    public async Task Suggest_NeverReturnsMoreThanTheLimit()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        for (var i = 1; i <= 12; i++)
        {
            await ListingSeeder.ListableAsync(host, i, "GIRLS", "TOPS", [InStock], name: $"Cotton top {i}");
        }

        var result = await SuggestAsync(host, "cotton");

        Assert.Equal(8, result.Items.Count);
    }

    [Fact]
    [Trait("Spec", "SRC-14")]
    public async Task Suggest_ProductWithNothingInStock_IsNotSuggested()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "TOPS", [new SeedVariant("RED", "5-6Y", 800m, 0)], name: "Cotton sold out");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "TOPS", [InStock], name: "Cotton ready");

        var result = await SuggestAsync(host, "cotton");

        Assert.Equal(["Cotton ready"], result.Items.Select(i => i.Label));
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public async Task Suggest_UsesSynonyms()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "TOPS", [InStock], name: "Cotton kameez");

        var result = await SuggestAsync(host, "kurt");

        Assert.Equal(["Cotton kameez"], result.Items.Select(i => i.Label));
    }

    [Theory]
    [Trait("Spec", "SRC-3")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("' OR 1=1 --")]
    [InlineData("<script>")]
    [InlineData("& | ! ( ) :*")]
    [InlineData("\0‮")]
    public async Task Suggest_EmptyOrHostileText_AnswersWithoutError(string? text)
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "TOPS", [InStock], name: "Cotton top");

        var result = await SuggestAsync(host, text);

        Assert.NotNull(result.Items);
    }

    [Fact]
    [Trait("Spec", "SRC-7")]
    public async Task Suggest_SamePrefixAgain_IsServedFromTheCache()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "TOPS", [InStock], name: "Cotton top");
        var first = await SuggestAsync(host, "cotton");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "TOPS", [InStock], name: "Cotton skirt");

        var again = await SuggestAsync(host, "  COTTON ");
        var other = await SuggestAsync(host, "cotto");

        Assert.Equal(first.Items.Count, again.Items.Count);
        Assert.Equal(2, other.Items.Count);
    }
}
