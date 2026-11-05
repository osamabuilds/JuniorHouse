using Romp.Modules.Storefront.Application.CollectionsPage;
using Romp.Modules.Storefront.Infrastructure;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.CollectionsPage;

/// <summary>
/// The collections index (docs/specs/storefront/collections-page, COL-1..3, 6): the enabled collections that have at least one live product,
/// in lookup order, with the number of pieces and the picture of the newest product, a page at a time and in a fixed number of queries.
/// </summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class GetCollectionsQueryTests(StorefrontPostgres postgres)
{
    private static async Task<StorefrontHost> HostAsync(StorefrontPostgres postgres)
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        // SUMMER (sort 10) comes from the seeder. WINTER and AUTUMN share sort 5 so the name breaks the tie; ARCHIVE is switched off.
        host.Lookups.Collections.AddRange([new(2, "WINTER", "Winter", 5, true), new(3, "AUTUMN", "Autumn", 5, true), new(4, "ARCHIVE", "Archive", 40, false)]);
        return host;
    }

    private static async Task<CollectionsPageDto> PageAsync(StorefrontHost host, int page = 1, int pageSize = 24) =>
        await host.SendAsync(new GetCollectionsQuery(page, pageSize));

    [Fact]
    [Trait("Spec", "COL-1")]
    [Trait("Spec", "COL-2")]
    public async Task ListsOnlyCollectionsWithLiveProducts_WithThePieceCountAndTheirOwnAddress()
    {
        using var host = await HostAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", collection: "SUMMER");
        await ListingSeeder.ListableAsync(host, 2, "BOYS", "TOPS", collection: "SUMMER");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES"); // in no collection

        var page = await PageAsync(host);

        var summer = Assert.Single(page.Items);
        Assert.Equal(("SUMMER", "Summer", "/collections/summer", 2), (summer.Code, summer.Name, summer.Path, summer.Pieces));
        Assert.DoesNotContain(page.Items, i => i.Code == "WINTER"); // a lookup row with no product is not listed
    }

    [Fact]
    [Trait("Spec", "COL-3")]
    public async Task ACollectionSwitchedOff_IsNeverListed_EvenWithProducts()
    {
        using var host = await HostAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", collection: "SUMMER");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", collection: "ARCHIVE");

        var page = await PageAsync(host);

        Assert.Equal(["SUMMER"], page.Items.Select(i => i.Code).ToArray());
    }

    [Fact]
    [Trait("Spec", "COL-1")]
    public async Task Collections_FollowTheLookupOrder_ThenTheirName()
    {
        using var host = await HostAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", collection: "SUMMER");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", collection: "WINTER");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES", collection: "AUTUMN");

        var page = await PageAsync(host);

        // Sort 5 first (Autumn before Winter by name), then Summer at sort 10.
        Assert.Equal(["AUTUMN", "WINTER", "SUMMER"], page.Items.Select(i => i.Code).ToArray());
    }

    [Fact]
    [Trait("Spec", "COL-6")]
    public async Task WhenNothingQualifies_ThePageIsEmptyButNormal()
    {
        using var host = await HostAsync(postgres);

        var page = await PageAsync(host);

        Assert.Empty(page.Items);
        Assert.Equal((0, 1, 1), (page.Total, page.Page, page.TotalPages));
    }

    [Fact]
    [Trait("Spec", "COL-1")]
    public async Task Paging_IsClamped_AndAPageAfterTheLastIsEmptyNotAnError()
    {
        using var host = await HostAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", collection: "SUMMER");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", collection: "WINTER");
        await ListingSeeder.ListableAsync(host, 3, "GIRLS", "DRESSES", collection: "AUTUMN");

        var first = await PageAsync(host, 1, 2);
        var second = await PageAsync(host, 2, 2);
        var beyond = await PageAsync(host, 99, 2);
        var silly = await PageAsync(host, -5, 100_000);

        Assert.Equal((2, 3, 2), (first.Items.Count, first.Total, first.TotalPages));
        Assert.Equal(["SUMMER"], second.Items.Select(i => i.Code).ToArray());
        Assert.Empty(beyond.Items);
        Assert.Equal(3, beyond.Total);
        Assert.Equal((1, 48), (silly.Page, silly.PageSize));
    }

    [Fact]
    [Trait("Spec", "COL-1")]
    public async Task TheTilePicture_IsTheNewestProductsMainPicture_AndMissingWhenThereIsNone()
    {
        using var host = await HostAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", collection: "SUMMER", publishedAt: DateTimeOffset.UtcNow.AddDays(-10), mainFile: "images/a.jpg");
        await ListingSeeder.ListableAsync(host, 2, "GIRLS", "DRESSES", collection: "SUMMER", publishedAt: DateTimeOffset.UtcNow.AddDays(-1), mainFile: "images/c.jpg");

        var summer = Assert.Single((await PageAsync(host)).Items);

        Assert.NotNull(summer.Image);
        Assert.NotEmpty(summer.Image!.Sources);
        var newest = (await host.SendAsync(new Romp.Modules.Storefront.Application.Listings.SearchListingQuery(
            Romp.Modules.Storefront.Domain.Listings.ListingScope.OfCollection("summer"),
            Romp.Modules.Storefront.Domain.Listings.ListingFilter.Parse(null, null, null, null, null, null, null).Filter!))).Value!.Tiles;
        Assert.Equal(newest.Single(t => t.Path.EndsWith("/p-2", StringComparison.Ordinal)).Image!.Sources[0].Webp, summer.Image.Sources[0].Webp);
    }

    [Fact]
    [Trait("Spec", "COL-1")]
    public async Task TheIndex_CostsAFixedNumberOfQueries_WhateverTheNumberOfCollections()
    {
        async Task<int> CountAsync(params string[] collections)
        {
            using var host = await HostAsync(postgres);
            for (var i = 0; i < collections.Length; i++)
            {
                await ListingSeeder.ListableAsync(host, i + 1, "GIRLS", "DRESSES", collection: collections[i]);
            }

            var counter = new QueryCounter();
            await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
            var handler = new GetCollectionsQueryHandler(context, host.Lookups, new CatalogLookupCache(TimeProvider.System));
            await handler.Handle(new GetCollectionsQuery(1, 24), default);
            counter.Reset();
            var page = await handler.Handle(new GetCollectionsQuery(1, 24), default);
            Assert.Equal(collections.Length, page.Items.Count);
            return counter.Count;
        }

        Assert.Equal(await CountAsync("SUMMER"), await CountAsync("SUMMER", "WINTER", "AUTUMN"));
    }
}
