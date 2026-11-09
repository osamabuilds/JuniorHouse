using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Storefront.Application.ProductRecommendations;
using Romp.Modules.Storefront.Infrastructure;
using Romp.Modules.Storefront.Infrastructure.ProductRecommendations;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.ProductRecommendations;

/// <summary>Recommendations tasks S1 to S5 (REC-2, REC-3, REC-4, REC-5, REC-13): the sales sync, the group refresh, choosing groups and the worker. Real PostgreSQL.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class RecommendationRefreshTests(StorefrontPostgres postgres)
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);

    private static DateOnly Day(int daysBefore) => DateOnly.FromDateTime(T0.UtcDateTime).AddDays(-daysBefore);

    private async Task<StorefrontHost> HostAsync()
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        host.Clock.Fixed = T0;
        return host;
    }

    internal static async Task SeedAsync(
        StorefrontHost host, long id, string gender = "GIRLS", string age = "KIDS", string category = "DRESSES", string colour = "RED", decimal price = 800m, int stock = 10) =>
        await ListingSeeder.ListableAsync(host, id, gender, category, [new SeedVariant(colour, "5-6Y", price, stock)], age: age);

    private static async Task<List<(long Product, long Similar, int Rank)>> RowsAsync(StorefrontHost host) =>
        (await host.QueryAsync(db => db.SimilarProducts.AsNoTracking().OrderBy(s => s.ProductId).ThenBy(s => s.Rank).ToListAsync()))
        .Select(s => (s.ProductId, s.SimilarProductId, (int)s.Rank)).ToList();

    // ---- S1: sales sync ----

    [Fact]
    [Trait("Spec", "REC-4")]
    public async Task Sync_CopiesTheWindowFromTheContract_AndIgnoresProductsNotInTheReadModel()
    {
        using var host = await HostAsync();
        await SeedAsync(host, 1);
        host.Sales.Rows.AddRange([new(1, Day(0), 3), new(1, Day(13), 2), new(1, Day(14), 9), new(999, Day(1), 5)]);

        await host.SendAsync(new SyncProductSalesCommand());

        var rows = await host.QueryAsync(db => db.ProductSaleDays.AsNoTracking().OrderBy(r => r.Day).ToListAsync());
        Assert.Equal([(1L, Day(13), 2), (1L, Day(0), 3)], rows.Select(r => (r.ProductId, r.Day, r.Units)).ToArray());
    }

    [Fact]
    [Trait("Spec", "REC-4")]
    public async Task Sync_RewritesADayWhoseCountDropped_AndRemovesOldDays_AndIsRepeatable()
    {
        using var host = await HostAsync();
        await SeedAsync(host, 1);
        host.Sales.Rows.AddRange([new(1, Day(2), 5), new(1, Day(1), 4)]);
        await host.SendAsync(new SyncProductSalesCommand());

        // A cancellation lowers day 2 and empties day 1; a week later day 2 has also left the window.
        host.Sales.Rows.Clear();
        host.Sales.Rows.Add(new(1, Day(2), 3));
        await host.SendAsync(new SyncProductSalesCommand());
        var afterCancel = await host.QueryAsync(db => db.ProductSaleDays.AsNoTracking().Select(r => new { r.Day, r.Units }).ToListAsync());
        Assert.Equal(Day(2), Assert.Single(afterCancel).Day);
        Assert.Equal(3, afterCancel[0].Units);

        host.Clock.Fixed = T0.AddDays(13);
        var first = await host.SendAsync(new SyncProductSalesCommand());
        var again = await host.SendAsync(new SyncProductSalesCommand());

        Assert.Equal(1, first);
        Assert.Equal(0, again);
        Assert.Equal(0, await host.QueryAsync(db => db.ProductSaleDays.CountAsync()));
    }

    // ---- S2: the meaning provider and its weights ----

    [Fact]
    [Trait("Spec", "REC-13")]
    public async Task TheDefaultProvider_IsUnavailable_AndTheDefaultOptionsAreValid()
    {
        var provider = new NoTextSimilarityProvider();

        Assert.False(provider.IsAvailable);
        Assert.Null(await provider.ScoreAsync(1, [2, 3], default));
        Assert.Equal(ValidateOptionsResult.Success, new StorefrontOptionsValidator().Validate(null, new StorefrontOptions()));
    }

    [Theory]
    [Trait("Spec", "REC-13")]
    [InlineData(-0.1, 0.5, 0.5)]
    [InlineData(0, 0, 0)]
    public void Weights_ThatAreNegativeOrAllZero_FailStartupValidation(double field, double meaning, double sales)
    {
        var options = new StorefrontOptions { RecommendationFieldWeight = field, RecommendationMeaningWeight = meaning, RecommendationSalesWeight = sales };

        var result = new StorefrontOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("RecommendationFieldWeight", StringComparison.Ordinal));
    }

    // ---- S3: refreshing one group ----

    [Fact]
    [Trait("Spec", "REC-2")]
    public async Task AGirlsToddlerDress_GetsItsOwnGroupFirst_ThenSameGender_ThenSameAge_ThenTheRest_EvenWhenAnOutsiderSellsBetter()
    {
        using var host = await HostAsync();
        await SeedAsync(host, 1);
        await SeedAsync(host, 2);
        await SeedAsync(host, 3, gender: "BOYS");
        await SeedAsync(host, 4, age: "AGE_3Y");
        await SeedAsync(host, 5, gender: "BOYS", age: "AGE_3Y");
        host.Sales.Rows.AddRange([new(3, Day(1), 90), new(4, Day(1), 50), new(5, Day(1), 70)]);
        await host.SendAsync(new SyncProductSalesCommand());

        var written = await host.SendAsync(new RefreshSimilarProductsCommand("GIRLS", "KIDS"));

        Assert.Equal(2, written); // rows are written for the group only
        var forOne = (await RowsAsync(host)).Where(r => r.Product == 1).Select(r => r.Similar).ToArray();
        Assert.Equal([2L, 4L, 3L, 5L], forOne);
        Assert.DoesNotContain(await RowsAsync(host), r => r.Product is 3 or 4 or 5);
    }

    [Fact]
    [Trait("Spec", "REC-3")]
    public async Task OfTwoEquallyCloseProducts_TheBetterSellerRanksFirst()
    {
        using var host = await HostAsync();
        await SeedAsync(host, 1);
        await SeedAsync(host, 2);
        await SeedAsync(host, 3);
        host.Sales.Rows.Add(new(3, Day(1), 8));
        await host.SendAsync(new SyncProductSalesCommand());

        await host.SendAsync(new RefreshSimilarProductsCommand("GIRLS", "KIDS"));

        var forOne = (await RowsAsync(host)).Where(r => r.Product == 1).Select(r => r.Similar).ToArray();
        Assert.Equal([3L, 2L], forOne);
    }

    [Fact]
    [Trait("Spec", "REC-5")]
    public async Task EachProduct_GetsAtMostTwelveRows_AndNeverItself()
    {
        using var host = await HostAsync();
        for (var id = 1; id <= 15; id++)
        {
            await SeedAsync(host, id);
        }

        await host.SendAsync(new RefreshSimilarProductsCommand("GIRLS", "KIDS"));

        var rows = await RowsAsync(host);
        foreach (var group in rows.GroupBy(r => r.Product))
        {
            Assert.Equal(12, group.Count());
            Assert.DoesNotContain(group, r => r.Similar == r.Product);
            Assert.Equal(Enumerable.Range(1, 12), group.Select(r => r.Rank).Order());
        }
    }

    [Fact]
    [Trait("Spec", "REC-13")]
    public async Task WithTheMeaningProviderOn_ATextCloseProductMovesUp_AndOffTheSameGroupStillGetsRows()
    {
        using var host = await HostAsync();
        await SeedAsync(host, 1);
        await SeedAsync(host, 2);
        await SeedAsync(host, 3);

        await host.SendAsync(new RefreshSimilarProductsCommand("GIRLS", "KIDS"));
        var off = (await RowsAsync(host)).Where(r => r.Product == 1).Select(r => r.Similar).ToArray();

        host.Meaning.IsAvailable = true;
        host.Meaning.Scores[3] = 1.0;
        await host.SendAsync(new RefreshSimilarProductsCommand("GIRLS", "KIDS"));
        var on = (await RowsAsync(host)).Where(r => r.Product == 1).Select(r => r.Similar).ToArray();

        Assert.Equal([2L, 3L], off);
        Assert.Equal([3L, 2L], on);
    }

    [Fact]
    [Trait("Spec", "REC-3")]
    public async Task ReplacingARowsIsOneSave_AFailureMidWayLeavesTheOldRows()
    {
        using var host = await HostAsync();
        await SeedAsync(host, 1);
        await SeedAsync(host, 2);
        await SeedAsync(host, 3);
        await host.SendAsync(new RefreshSimilarProductsCommand("GIRLS", "KIDS"));
        var before = await RowsAsync(host);

        host.Meaning.IsAvailable = true;
        host.Meaning.FailFor.Add(3);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.SendAsync(new RefreshSimilarProductsCommand("GIRLS", "KIDS")));

        Assert.Equal(before, await RowsAsync(host));
    }

    // ---- S4: choosing groups ----

    private static async Task TouchAsync(StorefrontHost host, long productId, DateTimeOffset at) =>
        await host.QueryAsync(db => db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdtDte, at)));

    [Fact]
    [Trait("Spec", "REC-5")]
    public async Task APass_RescoresOnlyChangedGroups_UntilADayHasPassed()
    {
        using var host = await HostAsync();
        await SeedAsync(host, 1);
        await SeedAsync(host, 2);
        await SeedAsync(host, 3, gender: "BOYS");
        await SeedAsync(host, 4, gender: "BOYS");

        host.Clock.Fixed = T0.AddHours(1);
        var first = await host.SendAsync(new RunRecommendationPassCommand());
        host.Clock.Fixed = T0.AddHours(2);
        var quiet = await host.SendAsync(new RunRecommendationPassCommand());
        await TouchAsync(host, 3, T0.AddHours(2).AddMinutes(30));
        host.Clock.Fixed = T0.AddHours(3);
        var changed = await host.SendAsync(new RunRecommendationPassCommand());
        host.Clock.Fixed = T0.AddDays(2);
        var daily = await host.SendAsync(new RunRecommendationPassCommand());

        Assert.Equal(2, first.Groups);
        Assert.Equal(0, quiet.Groups);
        Assert.Equal(1, changed.Groups);
        Assert.Equal(2, daily.Groups);
    }

    [Fact]
    [Trait("Spec", "REC-3")]
    public async Task APassWithAFailedGroup_IsLeftUnfinished_SoTheWatermarkDoesNotMove()
    {
        using var host = await HostAsync();
        await SeedAsync(host, 1);
        await SeedAsync(host, 2);
        await SeedAsync(host, 3, gender: "BOYS");
        await SeedAsync(host, 4, gender: "BOYS");
        host.Meaning.IsAvailable = true;
        host.Meaning.FailFor.Add(3);

        host.Clock.Fixed = T0.AddHours(1);
        var failing = await host.SendAsync(new RunRecommendationPassCommand());
        var finishedAfterFailure = await host.QueryAsync(db => db.RecommendationRuns.CountAsync(r => r.EndedAt != null));
        host.Meaning.FailFor.Clear();
        host.Clock.Fixed = T0.AddHours(2);
        var retry = await host.SendAsync(new RunRecommendationPassCommand());

        Assert.Equal(1, failing.Failed);
        Assert.Equal(1, failing.Groups);
        Assert.Equal(0, finishedAfterFailure);
        Assert.Equal(2, retry.Groups); // nothing finished before, so every group is still "changed"
        Assert.Equal(0, retry.Failed);
        Assert.Equal(1, await host.QueryAsync(db => db.RecommendationRuns.CountAsync(r => r.EndedAt != null)));
    }

    // ---- S5: the worker ----

    private static RecommendationRefreshWorker Worker(StorefrontHost host) =>
        new(host.Services.GetRequiredService<IServiceScopeFactory>(), Options.Create(new StorefrontOptions()), NullLogger<RecommendationRefreshWorker>.Instance);

    [Fact]
    [Trait("Spec", "REC-3")]
    public async Task TheWorker_RunsOnePass_SyncsSales_AndWritesAFinishedRun()
    {
        using var host = await HostAsync();
        await SeedAsync(host, 1);
        await SeedAsync(host, 2);
        host.Sales.Rows.Add(new ProductDayUnits(2, Day(1), 4));

        var result = await Worker(host).RunOnceAsync(default);

        Assert.Equal(1, result!.Groups);
        Assert.Equal(4, await host.QueryAsync(db => db.ProductSaleDays.SumAsync(r => r.Units)));
        var run = Assert.Single(await host.QueryAsync(db => db.RecommendationRuns.AsNoTracking().ToListAsync()));
        Assert.True(run.IsFinished);
        Assert.Equal(2, run.ProductCount);
    }

    [Fact]
    [Trait("Spec", "REC-10")]
    public async Task TheWorker_SurvivesAFailingGroup_AndGoesOn()
    {
        using var host = await HostAsync();
        await SeedAsync(host, 1);
        await SeedAsync(host, 2);
        await SeedAsync(host, 3, gender: "BOYS");
        await SeedAsync(host, 4, gender: "BOYS");
        host.Meaning.IsAvailable = true;
        host.Meaning.FailFor.Add(3);

        var result = await Worker(host).RunOnceAsync(default);

        Assert.Equal(1, result!.Failed);
        Assert.Equal(1, result.Groups);
        Assert.Contains(await RowsAsync(host), r => r.Product is 1 or 2);
    }
}
