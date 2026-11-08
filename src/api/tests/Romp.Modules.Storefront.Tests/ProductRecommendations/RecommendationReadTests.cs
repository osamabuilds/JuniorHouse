using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Application.ProductRecommendations;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Tests.ProductRecommendations;

/// <summary>Recommendations tasks Q1 to Q5 and E2 (REC-1, REC-4 to REC-8, REC-10 to REC-12): the page query, its tiles, failure isolation, speed and the event counter.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class RecommendationReadTests(StorefrontPostgres postgres)
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);

    private static DateOnly Day(int daysBefore) => DateOnly.FromDateTime(T0.UtcDateTime).AddDays(-daysBefore);

    private static string PathOf(long id, string gender = "girls") => $"/{gender}/dresses/p-{id}";

    private async Task<StorefrontHost> HostAsync()
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        host.Clock.Fixed = T0;
        return host;
    }

    private static Task SeedAsync(StorefrontHost host, long id, string gender = "GIRLS", int stock = 10, string colour = "RED") =>
        RecommendationRefreshTests.SeedAsync(host, id, gender: gender, stock: stock, colour: colour);

    private static async Task<StorefrontHost> GroupAsync(StorefrontPostgres postgres, int girls, bool refresh = true)
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        host.Clock.Fixed = T0;
        for (var id = 1; id <= girls; id++)
        {
            await SeedAsync(host, id);
        }

        if (refresh)
        {
            await host.SendAsync(new RefreshSimilarProductsCommand("GIRLS", "KIDS"));
        }

        return host;
    }

    private static async Task SoldOutAsync(StorefrontHost host, long productId) =>
        await host.SendAsync(new Romp.Modules.Storefront.Application.PublishedCatalog.ApplyAvailabilityCommand(new StockAvailability(productId * 100, $"SKU-{productId}-0", 0, 2)));

    private static long[] Ids(IEnumerable<ListingTileDto> tiles) => [.. tiles.Select(t => t.ProductId)];

    // ---- Q1: the tiles are the listing's tiles ----

    [Fact]
    [Trait("Spec", "REC-6")]
    [Trait("Spec", "REC-7")]
    public async Task ARecommendationTile_HasTheSameSizesAndVariantIdsAsTheListingTile()
    {
        using var host = await GroupAsync(postgres, 3);

        var recommendations = (await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!;
        var listing = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), ListingFilter.Parse(null, null, null, null, null, null, null).Filter!));

        var tile = recommendations.Similar.Single(t => t.ProductId == 2);
        var same = listing.Value!.Tiles.Single(t => t.ProductId == 2);
        Assert.Equal(same.Sizes.Select(s => (s.Code, s.VariantId, s.SoldOut)), tile.Sizes.Select(s => (s.Code, s.VariantId, s.SoldOut)));
        Assert.Equal(same.Price, tile.Price);
        Assert.Equal(same.SoldOut, tile.SoldOut);
    }

    [Fact]
    [Trait("Spec", "REC-8")]
    public async Task ATileWhoseSizesAreAllSoldOut_SaysSoldOut()
    {
        using var host = await GroupAsync(postgres, 3);
        await host.SendAsync(new Romp.Modules.Storefront.Application.PublishedCatalog.ApplyAvailabilityCommand(new StockAvailability(200, "SKU-2-0", 0, 2)));
        // The product is sold out, so the similar row drops it; the tile builder itself still flags it (used for a direct id lookup).
        var recommendations = (await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!;

        Assert.DoesNotContain(2, Ids(recommendations.Similar));
        var listing = await host.SendAsync(new SearchListingQuery(ListingScope.Audience("girls"), ListingFilter.Parse(null, null, null, null, null, null, null).Filter!));
        Assert.True(listing.Value!.Tiles.Single(t => t.ProductId == 2).SoldOut);
    }

    // ---- Q2: the similar row ----

    [Fact]
    [Trait("Spec", "REC-1")]
    public async Task TheSimilarRow_HoldsUpToFour_NeverTheProductItself()
    {
        using var host = await GroupAsync(postgres, 8);

        var result = (await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!;

        Assert.Equal(4, result.Similar.Count);
        Assert.DoesNotContain(1, Ids(result.Similar));
    }

    [Fact]
    [Trait("Spec", "REC-5")]
    public async Task ASoldOutProduct_LeavesTheRowAtOnce_AndTheNextOneFillsThePlace()
    {
        using var host = await GroupAsync(postgres, 7);
        var before = Ids((await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!.Similar);

        await SoldOutAsync(host, before[0]);
        var after = Ids((await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!.Similar);

        Assert.DoesNotContain(before[0], after);
        Assert.Equal(4, after.Length);
        Assert.Equal(before.Skip(1), after.Take(3));
    }

    [Fact]
    [Trait("Spec", "REC-5")]
    public async Task AnUnpublishedProduct_IsGone_AndFewerThanFourReturnsFewer()
    {
        using var host = await GroupAsync(postgres, 4);
        await host.QueryAsync(db => db.Products.Where(p => p.Id == 2).ExecuteDeleteAsync());

        var result = (await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!;

        Assert.Equal([3L, 4L], Ids(result.Similar).Order().ToArray());
    }

    [Fact]
    [Trait("Spec", "REC-10")]
    public async Task AProductWithNoSimilarRows_GetsAnEmptyRow_AndAnUnknownPathIsNull()
    {
        using var host = await GroupAsync(postgres, 1);

        var result = (await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!;

        Assert.Empty(result.Similar);
        Assert.Null(await host.SendAsync(new GetProductRecommendationsQuery("/girls/dresses/nothing")));
        Assert.Null(await host.SendAsync(new GetProductRecommendationsQuery("not a path?")));
    }

    // ---- Q3: the trending row ----

    private static async Task SellAsync(StorefrontHost host, params (long Product, int Units)[] sales)
    {
        host.Sales.Rows.AddRange(sales.Select(s => new ProductDayUnits(s.Product, Day(1), s.Units)));
        await host.SendAsync(new SyncProductSalesCommand());
    }

    [Fact]
    [Trait("Spec", "REC-4")]
    public async Task Trending_IsRankedByUnits_SameGroupFirst_ThenFilledFromTheWholeShop()
    {
        using var host = await GroupAsync(postgres, 3, refresh: false);
        await SeedAsync(host, 8, gender: "BOYS");
        await SeedAsync(host, 9, gender: "BOYS");
        await SellAsync(host, (2, 5), (3, 9), (8, 40), (9, 30));

        var result = (await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!;

        // Same gender and age group first (3 then 2), then the shop's best of the rest (8 then 9). Never the product itself.
        Assert.Equal([3L, 2L, 8L, 9L], Ids(result.Trending));
    }

    [Fact]
    [Trait("Spec", "REC-4")]
    public async Task Trending_LeavesOutTheSimilarFour_SoldOutProducts_AndProductsWithoutSales()
    {
        using var host = await GroupAsync(postgres, 8);
        var similar = Ids((await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!.Similar);
        var others = Enumerable.Range(2, 7).Select(i => (long)i).Except(similar).ToArray();
        await SellAsync(host, [.. similar.Select(s => (s, 50)), .. others.Select(o => (o, 3))]);
        await SoldOutAsync(host, others[0]);

        var trending = Ids((await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!.Trending);

        Assert.Empty(trending.Intersect(similar));
        Assert.DoesNotContain(others[0], trending);
        Assert.DoesNotContain(1, trending);
        Assert.Equal(others.Skip(1).Order(), trending.Order());
    }

    [Fact]
    [Trait("Spec", "REC-4")]
    public async Task Trending_IgnoresSalesOutsideTheWindow()
    {
        using var host = await GroupAsync(postgres, 3, refresh: false);
        host.Sales.Rows.Add(new ProductDayUnits(2, Day(20), 99));
        await host.SendAsync(new SyncProductSalesCommand());

        var result = (await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!;

        Assert.Empty(result.Trending);
    }

    // ---- Q4: failure isolation ----

    private static async Task DropAsync(StorefrontHost host, string table) =>
        await host.QueryAsync(db => table switch
        {
            "PRDT_SMLR" => db.Database.ExecuteSqlRawAsync("DROP TABLE \"STFR\".\"PRDT_SMLR\" CASCADE"),
            "PRDT_SALE_DAY" => db.Database.ExecuteSqlRawAsync("DROP TABLE \"STFR\".\"PRDT_SALE_DAY\" CASCADE"),
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        });

    [Fact]
    [Trait("Spec", "REC-10")]
    public async Task WhenTheSimilarReadFails_TrendingStillShows_AndNothingIsThrown()
    {
        using var host = await GroupAsync(postgres, 4, refresh: false);
        await SellAsync(host, (2, 5), (3, 4));
        await DropAsync(host, "PRDT_SMLR");

        var result = (await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!;

        Assert.Empty(result.Similar);
        Assert.Equal([2L, 3L], Ids(result.Trending));
    }

    [Fact]
    [Trait("Spec", "REC-10")]
    public async Task WhenTheTrendingReadFails_TheSimilarRowStillShows()
    {
        using var host = await GroupAsync(postgres, 4);
        await DropAsync(host, "PRDT_SALE_DAY");

        var result = (await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!;

        Assert.Equal(3, result.Similar.Count);
        Assert.Empty(result.Trending);
    }

    [Fact]
    [Trait("Spec", "REC-10")]
    public async Task WithNoPrecomputedRowsAtAll_OnlyTrendingIsReturned()
    {
        using var host = await GroupAsync(postgres, 3, refresh: false);
        await SellAsync(host, (2, 5));

        var result = (await host.SendAsync(new GetProductRecommendationsQuery(PathOf(1))))!;

        Assert.Empty(result.Similar);
        Assert.Equal([2L], Ids(result.Trending));
    }

    // ---- Q5: speed ----

    [Fact]
    [Trait("Spec", "REC-11")]
    public async Task TheQuery_IsAConstantNumberOfQueries_AndAnswersWithinTheBudget()
    {
        using var host = await GroupAsync(postgres, 14);
        await SellAsync(host, Enumerable.Range(2, 13).Select(i => ((long)i, i)).ToArray());
        var counter = new QueryCounter();
        await using var db = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var handler = new GetProductRecommendationsQueryHandler(
            db, new FakeListingSettings(), new FakeRecommendationSettings(), host.Clock, NullLogger<GetProductRecommendationsQueryHandler>.Instance);

        counter.Reset();
        var first = (await handler.Handle(new GetProductRecommendationsQuery(PathOf(1)), default))!;
        var queries = counter.Count;
        var times = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            var watch = Stopwatch.StartNew();
            await handler.Handle(new GetProductRecommendationsQuery(PathOf(1)), default);
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        Assert.Equal(4, first.Similar.Count);
        Assert.Equal(4, first.Trending.Count);
        Assert.True(queries <= 9, $"{queries} queries");
        Assert.True(times.Order().ElementAt(times.Count / 2) < 150, $"median {times.Order().ElementAt(times.Count / 2):F0} ms");
    }

    // ---- E2: the event counter ----

    private static async Task<(int Clicks, int Adds)> CountersAsync(StorefrontHost host, long productId, string row) =>
        await host.QueryAsync(async db =>
        {
            var rows = await db.RecommendationDays.AsNoTracking().Where(d => d.ProductId == productId).ToListAsync();
            var sum = rows.Where(d => d.Row == (row == "SIMILAR" ? Romp.Modules.Storefront.Domain.ProductRecommendations.RecommendationRowId.Similar : Romp.Modules.Storefront.Domain.ProductRecommendations.RecommendationRowId.Trending));
            return (sum.Sum(d => d.ClickCount), sum.Sum(d => d.AddCount));
        });

    [Fact]
    [Trait("Spec", "REC-12")]
    public async Task ClickAndAdd_IncrementTheDayCounters()
    {
        using var host = await GroupAsync(postgres, 2, refresh: false);

        Assert.Equal(RecommendationEventOutcome.Recorded, await host.SendAsync(new RecordRecommendationEventCommand("SIMILAR", 2, "click")));
        Assert.Equal(RecommendationEventOutcome.Recorded, await host.SendAsync(new RecordRecommendationEventCommand("SIMILAR", 2, "click")));
        Assert.Equal(RecommendationEventOutcome.Recorded, await host.SendAsync(new RecordRecommendationEventCommand("SIMILAR", 2, "add")));
        await host.SendAsync(new RecordRecommendationEventCommand("TRENDING", 2, "add"));

        Assert.Equal((2, 1), await CountersAsync(host, 2, "SIMILAR"));
        Assert.Equal((0, 1), await CountersAsync(host, 2, "TRENDING"));
    }

    [Fact]
    [Trait("Spec", "REC-12")]
    public async Task TwoConcurrentClicks_GiveTwoMore()
    {
        using var host = await GroupAsync(postgres, 2, refresh: false);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => host.SendAsync(new RecordRecommendationEventCommand("SIMILAR", 2, "click"))));

        Assert.Equal((20, 0), await CountersAsync(host, 2, "SIMILAR"));
    }

    [Theory]
    [Trait("Spec", "REC-12")]
    [InlineData("NOPE", 2L, "click")]
    [InlineData("SIMILAR", 2L, "hover")]
    [InlineData("SIMILAR", 0L, "click")]
    [InlineData("SIMILAR", -3L, "add")]
    [InlineData(null, 2L, "click")]
    [InlineData("SIMILAR", 2L, null)]
    public async Task AnUnknownRowOrAction_OrANonPositiveProduct_IsRejected(string? row, long productId, string? action)
    {
        using var host = await GroupAsync(postgres, 2, refresh: false);

        var outcome = await host.SendAsync(new RecordRecommendationEventCommand(row, productId, action));

        Assert.Equal(RecommendationEventOutcome.Invalid, outcome);
        Assert.Equal(0, await host.QueryAsync(db => db.RecommendationDays.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "REC-12")]
    public async Task AnUnknownProduct_IsNotRecorded_AndOnlyCountersAreStored()
    {
        using var host = await GroupAsync(postgres, 2, refresh: false);

        var outcome = await host.SendAsync(new RecordRecommendationEventCommand("SIMILAR", 777, "click"));

        Assert.Equal(RecommendationEventOutcome.UnknownProduct, outcome);
        Assert.Equal(0, await host.QueryAsync(db => db.RecommendationDays.CountAsync()));
        var columns = typeof(Romp.Modules.Storefront.Domain.ProductRecommendations.RecommendationDay).GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain(columns, c => c.Contains("Ip", StringComparison.Ordinal) || c.Contains("Cookie", StringComparison.Ordinal) || c.Contains("Customer", StringComparison.Ordinal) || c.Contains("Session", StringComparison.Ordinal));
    }
}
