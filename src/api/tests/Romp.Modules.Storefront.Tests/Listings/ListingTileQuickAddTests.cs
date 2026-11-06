using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Infrastructure;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>
/// Product card quick add (docs/specs/storefront/product-card-quick-add, CRD-1..5, 7, 8): the tile carries what the card needs to add a size
/// to the bag without a request per card: a variant per size, its availability, a second picture and the New and Low stock facts.
/// </summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class ListingTileQuickAddTests(StorefrontPostgres postgres)
{
    private static ListingFilter Filter(string? colour = null) =>
        ListingFilter.Parse(null, colour is null ? null : [colour], null, null, null, null, null).Filter!;

    private static async Task<ListingTileDto> TileAsync(StorefrontHost host, ListingFilter? filter = null)
    {
        var result = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), filter ?? Filter()));
        Assert.Equal(ListingOutcome.Found, result.Outcome);
        return Assert.Single(result.Value!.Tiles);
    }

    [Fact]
    [Trait("Spec", "CRD-4")]
    public async Task EachSize_CarriesAVariantOfTheTilesDefaultColour_TheOneThatGivesTheTileItsPrice()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        // Variant ids are 100 + index: RED 100, 101 and NAVY 102, 103. Navy is cheaper, so navy is the colour the card shows.
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES",
        [
            new("RED", "3-4Y", 800m, 5), new("RED", "5-6Y", 800m, 5), new("NAVY", "3-4Y", 700m, 5), new("NAVY", "5-6Y", 700m, 5),
        ]);

        var tile = await TileAsync(host);

        Assert.Equal([("3-4Y", 102L, "NAVY"), ("5-6Y", 103L, "NAVY")], tile.Sizes.Select(s => (s.Code, s.VariantId, s.ColourName)).ToArray());
    }

    [Fact]
    [Trait("Spec", "CRD-4")]
    public async Task WhenTheVisitorFilteredByColour_TheCardAddsThatColour()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES",
        [
            new("RED", "3-4Y", 800m, 5), new("RED", "5-6Y", 800m, 5), new("NAVY", "3-4Y", 700m, 5), new("NAVY", "5-6Y", 700m, 5),
        ]);

        var tile = await TileAsync(host, Filter(colour: "red"));

        Assert.Equal([100L, 101L], tile.Sizes.Select(s => s.VariantId).ToArray());
        Assert.All(tile.Sizes, s => Assert.Equal("RED", s.ColourName));
    }

    [Fact]
    [Trait("Spec", "CRD-4")]
    public async Task ASizeTheDefaultColourDoesNotHave_UsesTheCheapestInStockVariantOfAnotherColour_AndNamesIt()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        // Navy only comes in 5-6Y and is cheaper; 3-4Y exists only in red.
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "3-4Y", 800m, 5), new("NAVY", "5-6Y", 700m, 5)]);

        var tile = await TileAsync(host);

        Assert.Equal([("3-4Y", 100L, "RED"), ("5-6Y", 101L, "NAVY")], tile.Sizes.Select(s => (s.Code, s.VariantId, s.ColourName)).ToArray());
    }

    [Fact]
    [Trait("Spec", "CRD-3")]
    [Trait("Spec", "CRD-4")]
    public async Task Sizes_SayInStockLowStockOrSoldOut_AndTheTileHasLowStockWhenAnySizeIsLow()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        // The test threshold is 5: up to and including 5 units is low.
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "3-4Y", 800m, 3), new("RED", "5-6Y", 800m, 0), new("RED", "9-10Y", 800m, 20)]);

        var tile = await TileAsync(host);

        Assert.Equal([("3-4Y", "LowStock"), ("5-6Y", "SoldOut"), ("9-10Y", "InStock")], tile.Sizes.Select(s => (s.Code, s.Availability)).ToArray());
        Assert.True(tile.HasLowStock);
    }

    [Fact]
    [Trait("Spec", "CRD-3")]
    public async Task ATileWithPlentyOfStock_HasNoLowStockFlag()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new("RED", "3-4Y", 800m, 20), new("RED", "5-6Y", 800m, 30)]);

        Assert.False((await TileAsync(host)).HasLowStock);
    }

    [Fact]
    [Trait("Spec", "CRD-3")]
    public async Task ATile_IsNewInsideTheNewInWindow_AndNotOutsideIt()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-5), name: "Fresh");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-90), name: "Old");

        var result = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), Filter()));

        var tiles = result.Value!.Tiles.ToDictionary(t => t.Name);
        Assert.True(tiles["Fresh"].IsNew);
        Assert.False(tiles["Old"].IsNew);
    }

    [Fact]
    [Trait("Spec", "CRD-1")]
    public async Task ATileWithSeveralPictures_HasTheNextOneAsItsSecondPicture_AndOnePictureMeansNone()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", name: "Two pictures", imageCount: 3);
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", name: "One picture", imageCount: 1);

        var result = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), Filter()));

        var tiles = result.Value!.Tiles.ToDictionary(t => t.Name);
        var second = tiles["Two pictures"].SecondImage;
        Assert.NotNull(second);
        Assert.NotEmpty(second!.Sources);
        Assert.NotEqual(tiles["Two pictures"].Image!.Sources[0].Webp, second.Sources[0].Webp);
        Assert.Null(tiles["One picture"].SecondImage);
    }

    [Fact]
    [Trait("Spec", "CRD-1")]
    public async Task TheListing_RunsTheSameNumberOfQueries_WhateverThePageHolds()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        for (var id = 1; id <= 6; id++)
        {
            await ListingSeeder.ListableAsync(host, id, "GIRLS", "DRESSES", [new("RED", "3-4Y", 800m, 3), new("NAVY", "5-6Y", 700m, 5)], name: $"Dress {id}", imageCount: 3);
        }

        var counter = new QueryCounter();
        await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var handler = new SearchListingQueryHandler(context, host.Lookups, new CatalogLookupCache(TimeProvider.System), new FakeListingSettings(), TimeProvider.System);

        await handler.Handle(new SearchListingQuery(ListingScope.Audience("girls"), Filter()), default);
        counter.Reset();
        var page = await handler.Handle(new SearchListingQuery(ListingScope.Audience("girls"), Filter()), default);

        Assert.Equal(6, page.Value!.Tiles.Count);
        // Count, page, images, derivatives, sizes: the same five queries as before the card gained a second picture and variants.
        Assert.Equal(5, counter.Count);
    }
}
