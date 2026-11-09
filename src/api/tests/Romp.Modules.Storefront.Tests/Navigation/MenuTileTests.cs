using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Application.Navigation;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Infrastructure;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Navigation;

/// <summary>
/// The mega menu image tile (docs/specs/storefront/mega-menu-tile, MGT-1..3): each audience's panel gets one picture tile, taken from the
/// audience's newest listable product, linking to its New in page. The menu is cached for a minute, so the tile costs a fixed few queries.
/// </summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class MenuTileTests(StorefrontPostgres postgres)
{
    private static MenuAudienceDto Audience(MenuDto menu, string code) => Assert.Single(menu.Audiences, a => a.Code == code);

    private static async Task<string> TilePictureAsync(StorefrontHost host, string productName)
    {
        var page = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), ListingFilter.Parse(null, null, null, null, null, null, null).Filter!));
        return page.Value!.Tiles.Single(t => t.Name == productName).Image!.Sources[0].Webp;
    }

    [Fact]
    [Trait("Spec", "MGT-1")]
    public async Task AnAudience_GetsATile_FromItsNewestListableProduct_WithTheFixedTextAndItsPicture()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-10), name: "Older", mainFile: "images/a.jpg");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-2), name: "Newer", mainFile: "images/c.jpg");

        var menu = await host.SendAsync(new GetMenuQuery());

        var tile = Audience(menu, "GIRLS").Tile;
        Assert.NotNull(tile);
        Assert.Equal(("New in", "Just arrived"), (tile!.Eyebrow, tile.Line));
        Assert.Equal(await TilePictureAsync(host, "Newer"), tile.Image.Sources[0].Webp);
        Assert.NotEqual(await TilePictureAsync(host, "Older"), tile.Image.Sources[0].Webp);
        Assert.False(string.IsNullOrWhiteSpace(tile.Image.AltText));
    }

    [Fact]
    [Trait("Spec", "MGT-2")]
    public async Task TheTile_LinksToTheAudiencesNewInPage()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-2));

        var tile = Audience(await host.SendAsync(new GetMenuQuery()), "GIRLS").Tile!;

        Assert.Equal("/girls/new-in", tile.Path);
    }

    [Fact]
    [Trait("Spec", "MGT-2")]
    public async Task WhenNothingIsNew_TheTileStillLinksToNewIn_WhichHandlesItsOwnEmptyState()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-200));

        var audience = Audience(await host.SendAsync(new GetMenuQuery()), "GIRLS");

        Assert.Equal("/girls/new-in", audience.Tile!.Path);
    }

    [Fact]
    [Trait("Spec", "MGT-3")]
    public async Task AnAudienceWithNoListableProduct_HasNoTile()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES");
        await ListingSeeder.ListableAsync(host, 2, "BOYS", "TOPS");
        await host.SendAsync(new Romp.Modules.Storefront.Application.PublishedCatalog.ApplyProductSnapshotCommand(2, null));

        var menu = await host.SendAsync(new GetMenuQuery());

        Assert.NotNull(Audience(menu, "GIRLS").Tile);
        Assert.DoesNotContain(menu.Audiences, a => a.Code == "BOYS");
    }

    [Fact]
    [Trait("Spec", "MGT-1")]
    public async Task EachAudienceGetsItsOwnTile_FromItsOwnProducts()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", mainFile: "images/a.jpg", name: "Girls product");
        await ListingSeeder.ListableAsync(host, 2, "BOYS", "TOPS", mainFile: "images/c.jpg", name: "Boys product");

        var menu = await host.SendAsync(new GetMenuQuery());

        Assert.NotEqual(Audience(menu, "GIRLS").Tile!.Image.Sources[0].Webp, Audience(menu, "BOYS").Tile!.Image.Sources[0].Webp);
    }

    [Fact]
    [Trait("Spec", "SLD-11")]
    public async Task TheTile_SkipsASoldOutNewestProduct_AndAnAudienceWithOnlySoldOutProductsGetsNone_NorMenuEntries()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", publishedAt: DateTimeOffset.UtcNow.AddDays(-10), name: "Older", mainFile: "images/a.jpg");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "TOPS", [new("RED", "5-6Y", 800m, 0)], publishedAt: DateTimeOffset.UtcNow.AddDays(-1), name: "Newest sold out", mainFile: "images/c.jpg");
        await ListingSeeder.ListableAsync(host, 3, "BOYS", "TOPS", [new("RED", "5-6Y", 800m, 0)], name: "Boys sold out");

        var menu = await host.SendAsync(new GetMenuQuery());

        var girls = Audience(menu, "GIRLS");
        Assert.Equal(await TilePictureAsync(host, "Older"), girls.Tile!.Image.Sources[0].Webp);
        Assert.DoesNotContain(girls.Entries, e => e.Code == "TOPS");
        Assert.DoesNotContain(menu.Audiences, a => a.Code == "BOYS");
    }

    [Fact]
    [Trait("Spec", "MGT-1")]
    public async Task TheTile_CostsAFixedNumberOfExtraQueries_WhateverTheNumberOfAudiences()
    {
        async Task<int> CountAsync(int audiences)
        {
            using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
            var genders = new[] { "GIRLS", "BOYS" };
            for (var i = 0; i < audiences; i++)
            {
                await ListingSeeder.ListableAsync(host, i + 1, genders[i], "DRESSES");
            }

            var counter = new QueryCounter();
            await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
            var handler = new GetMenuQueryHandler(context, host.Lookups, new CatalogLookupCache(TimeProvider.System), new FakeMenuSettings());
            await handler.Handle(new GetMenuQuery(), default);
            counter.Reset();
            var menu = await handler.Handle(new GetMenuQuery(), default);
            Assert.Equal(audiences, menu.Audiences.Count(a => a.Tile is not null));
            return counter.Count;
        }

        Assert.Equal(await CountAsync(1), await CountAsync(2));
    }
}
