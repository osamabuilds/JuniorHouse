using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Catalog.Application.SizeCharts;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.SizeCharts;

/// <summary>ISizeChartQueries (published-catalog task 5): the charts the storefront read model is built from (PCT-1, 5, 6).</summary>
[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class SizeChartQueriesTests(CatalogPostgres postgres)
{
    private const short Unisex = 3;
    private static long _categorySeed = 26_000;

    /// <summary>A category no other test uses: charts are unique per category and gender on a shared database.</summary>
    private static short FreshCategory(CatalogHost host)
    {
        var id = (short)Interlocked.Increment(ref _categorySeed);
        host.Lookups.Categories[id] = new(id, "CAT" + id, "Category " + id);
        return id;
    }

    private static SizeChartLineInput Line(short size, int ageMax = 24) => new(size, 12, ageMax, 80, 92, null, null, 52m, null, null);

    private static Task<T> QueriesAsync<T>(CatalogHost host, Func<ISizeChartQueries, Task<T>> action) =>
        host.InScopeAsync(sp => action(sp.GetRequiredService<ISizeChartQueries>()));

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task AChart_ComesWithItsCategoryGenderSizeNamesAndMeasurements()
    {
        using var host = new CatalogHost(postgres);
        var category = FreshCategory(host);
        var created = await host.SendAsync(new CreateSizeChartCommand(category, 2, "Dresses", [Line(5), Line(4)], "Test Person"));

        var chart = await QueriesAsync(host, q => q.GetChartAsync(category, 2, CancellationToken.None));

        Assert.NotNull(chart);
        Assert.Equal(created.Id, chart.ChartId);
        Assert.Equal("Dresses", chart.Name);
        Assert.Equal("Girls", chart.Gender.Name);
        Assert.Equal("Category " + category, chart.Category.Name);
        Assert.Equal([4, 5], chart.Lines.Select(l => (int)l.Size.Id));
        Assert.Equal("1-2 Years", chart.Lines[0].Size.Name);
        Assert.Equal((12, 24, 80m, 92m, 52m), (chart.Lines[0].AgeMinMonths, chart.Lines[0].AgeMaxMonths, chart.Lines[0].HeightMinCm, chart.Lines[0].HeightMaxCm, chart.Lines[0].ChestCm));
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task TheChartVersion_MovesWhenTheChartIsEdited()
    {
        using var host = new CatalogHost(postgres);
        var category = FreshCategory(host);
        var created = await host.SendAsync(new CreateSizeChartCommand(category, 2, "Dresses", [Line(4)], "Test Person"));
        var before = await QueriesAsync(host, q => q.GetChartAsync(category, 2, CancellationToken.None));

        await host.SendAsync(new UpdateSizeChartCommand(created.Id, "Dresses v2", [Line(4, ageMax: 30)], created.Version, "Test Person"));
        var after = await QueriesAsync(host, q => q.GetChartAsync(category, 2, CancellationToken.None));

        Assert.True(after!.Version > before!.Version);
        Assert.Equal(30, after.Lines[0].AgeMaxMonths);
    }

    [Fact]
    [Trait("Spec", "CHT-3")]
    public async Task AGenderWithoutItsOwnChart_GetsTheCategorysUnisexChart_AndTheAnswerSaysSo()
    {
        using var host = new CatalogHost(postgres);
        var category = FreshCategory(host);
        await host.SendAsync(new CreateSizeChartCommand(category, Unisex, "Unisex dresses", [Line(4)], "Test Person"));

        var chart = await QueriesAsync(host, q => q.GetChartAsync(category, 2, CancellationToken.None));

        Assert.NotNull(chart);
        Assert.Equal("Unisex dresses", chart.Name);
        Assert.Equal(Unisex, chart.Gender.Id); // not the girls' id that was asked for: the girls have no chart of their own
    }

    [Fact]
    [Trait("Spec", "CHT-3")]
    public async Task AGendersOwnChart_BeatsTheUnisexOne()
    {
        using var host = new CatalogHost(postgres);
        var category = FreshCategory(host);
        await host.SendAsync(new CreateSizeChartCommand(category, Unisex, "Unisex", [Line(4)], "Test Person"));
        await host.SendAsync(new CreateSizeChartCommand(category, 2, "Girls", [Line(4)], "Test Person"));

        var chart = await QueriesAsync(host, q => q.GetChartAsync(category, 2, CancellationToken.None));

        Assert.Equal("Girls", chart!.Name);
    }

    [Fact]
    [Trait("Spec", "CHT-3")]
    public async Task ACategoryWithNoChart_HasNone()
    {
        using var host = new CatalogHost(postgres);

        Assert.Null(await QueriesAsync(host, q => q.GetChartAsync(FreshCategory(host), 2, CancellationToken.None)));
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task ListCharts_PagesByIdWithoutRepeatsOrGaps_AndCostsTheSameQueriesPerPage()
    {
        var counter = new CommandCounter();
        using var host = new CatalogHost(postgres, counter: counter);
        var category = FreshCategory(host);
        var ids = new List<long>();
        foreach (short gender in new short[] { 1, 2, Unisex })
        {
            ids.Add((await host.SendAsync(new CreateSizeChartCommand(category, gender, "Chart " + gender, [Line(4), Line(5)], "Test Person"))).Id);
        }

        var after = ids[0] - 1;
        counter.Reset();
        var first = await QueriesAsync(host, q => q.ListChartsAsync(after, 2, CancellationToken.None));
        var queriesForTwo = counter.Count;
        counter.Reset();
        var second = await QueriesAsync(host, q => q.ListChartsAsync(first[^1].ChartId, 2, CancellationToken.None));
        var queriesForOne = counter.Count;

        Assert.Equal(ids.Take(2), first.Select(c => c.ChartId));
        Assert.Equal(ids[2], second[0].ChartId);
        Assert.All(first.Concat(second.Take(1)), c => Assert.Equal(2, c.Lines.Count));
        Assert.Equal(queriesForTwo, queriesForOne);
    }
}
