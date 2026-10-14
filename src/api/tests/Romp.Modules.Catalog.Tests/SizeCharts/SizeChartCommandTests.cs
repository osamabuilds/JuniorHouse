using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Catalog.Application.Products;
using Romp.Modules.Catalog.Application.SizeCharts;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.SizeCharts;

[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class SizeChartCommandTests(CatalogPostgres postgres)
{
    private static long _genderSeed = 20_000;

    /// <summary>A gender id no other test uses (charts are unique per category and gender on a shared database).</summary>
    private static short FreshGender(CatalogHost host)
    {
        var id = (short)Interlocked.Increment(ref _genderSeed);
        host.Lookups.Genders[id] = new(id, "G" + id, "Gender " + id);
        return id;
    }

    private static SizeChartLineInput Line(short size, int ageMin = 12, int ageMax = 24, decimal? hMin = 80, decimal? hMax = 92) =>
        new(size, ageMin, ageMax, hMin, hMax, null, null, null, null, null);

    [Fact]
    [Trait("Spec", "CHT-1")]
    public async Task ACreatedChart_ReturnsOneRowPerSize_WithNamesInSizeOrder()
    {
        using var host = new CatalogHost(postgres);
        var gender = FreshGender(host);

        var chart = await host.SendAsync(new CreateSizeChartCommand(3, gender, "Dresses", [Line(6), Line(4), Line(5)], "Test Person"));

        Assert.Equal([4, 5, 6], chart.Lines.Select(l => (int)l.SizeId).ToArray());
        Assert.Equal("1-2 Years", chart.Lines[0].SizeName);
        Assert.Equal("cm", chart.Unit);
        Assert.Equal(3, (await host.SendAsync(new GetSizeChartQuery(chart.Id))).Lines.Count);
    }

    [Fact]
    [Trait("Spec", "CHT-2")]
    public async Task CreatingAndEditingAChart_WritesTheChangedEvent_InTheSameTransaction()
    {
        using var host = new CatalogHost(postgres);
        var gender = FreshGender(host);
        var chart = await host.SendAsync(new CreateSizeChartCommand(3, gender, "Dresses", [Line(4)], "Test Person"));

        var edited = await host.SendAsync(new UpdateSizeChartCommand(chart.Id, "Dresses v2", [Line(4, hMax: 95), Line(5)], chart.Version, "Test Person"));

        Assert.Equal(2, edited.Lines.Count);
        Assert.Equal(95m, edited.Lines[0].HeightMaxCm);
        await using var db = postgres.NewContext();
        Assert.Equal(2, await db.Set<OutboxMessage>().CountAsync(m => m.EventType == "SizeChartChangedEvent" && m.AggregateId == chart.Id));
    }

    [Fact]
    [Trait("Spec", "CHT-2")]
    public async Task AStaleChartEdit_IsRefused()
    {
        using var host = new CatalogHost(postgres);
        var gender = FreshGender(host);
        var chart = await host.SendAsync(new CreateSizeChartCommand(3, gender, "Dresses", [Line(4)], "Test Person"));
        await host.SendAsync(new UpdateSizeChartCommand(chart.Id, "One", [Line(4)], chart.Version, "Editor One"));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            host.SendAsync(new UpdateSizeChartCommand(chart.Id, "Two", [Line(4)], chart.Version, "Editor Two")));
    }

    [Fact]
    [Trait("Spec", "CHT-4")]
    public async Task ARowWithMinAboveMax_OrNegative_OrNothing_IsRefused()
    {
        using var host = new CatalogHost(postgres);
        var gender = FreshGender(host);

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new CreateSizeChartCommand(3, gender, "Bad", [Line(4, hMin: 100, hMax: 90)], "Test Person")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new CreateSizeChartCommand(3, gender, "Bad", [Line(4, ageMin: -1)], "Test Person")));
        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new CreateSizeChartCommand(3, gender, "Bad", [new SizeChartLineInput(4, null, null, null, null, null, null, null, null, null)], "Test Person")));
    }

    [Fact]
    [Trait("Spec", "CHT-5")]
    public async Task TwoRowsForOneSize_AndASecondChartForTheSameCategoryAndGender_AreRefused()
    {
        using var host = new CatalogHost(postgres);
        var gender = FreshGender(host);

        await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new CreateSizeChartCommand(3, gender, "Dup", [Line(4), Line(4)], "Test Person")));
        await host.SendAsync(new CreateSizeChartCommand(3, gender, "First", [Line(4)], "Test Person"));
        await Assert.ThrowsAsync<DomainException>(() =>
            host.SendAsync(new CreateSizeChartCommand(3, gender, "Second", [Line(5)], "Test Person")));
    }

    [Fact]
    public async Task UnknownCategoryGenderOrSize_AreRefused()
    {
        using var host = new CatalogHost(postgres);

        var error = await Assert.ThrowsAsync<Romp.BuildingBlocks.Application.ValidationException>(() =>
            host.SendAsync(new CreateSizeChartCommand(99, 98, "Bad", [Line(77)], "Test Person")));

        Assert.Equal(["CategoryId", "GenderId", "Lines"], error.Errors.Keys.ToArray());
    }

    [Fact]
    [Trait("Spec", "CHT-3")]
    public async Task DeletingAChart_IsAllowed_ProductsThenReportNoChart_AndTheEventIsWritten()
    {
        using var host = new CatalogHost(postgres);
        var gender = FreshGender(host);
        var chart = await host.SendAsync(new CreateSizeChartCommand(3, gender, "Dresses", [Line(4)], "Test Person"));
        var product = await host.NewProductAsync(genderId: gender, categoryId: 3);
        Assert.True((await host.SendAsync(new GetChartForProductQuery(product.Id))).Found);

        await host.SendAsync(new DeleteSizeChartCommand(chart.Id, "Test Person"));

        Assert.False((await host.SendAsync(new GetChartForProductQuery(product.Id))).Found);
        await using var db = postgres.NewContext();
        Assert.False(await db.Charts.AnyAsync(c => c.Id == chart.Id));
        Assert.False(await db.ChartLines.AnyAsync(l => l.ChartId == chart.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetSizeChartQuery(chart.Id)));
    }

    [Fact]
    [Trait("Spec", "CHT-1")]
    public async Task TheProductsChart_IsLimitedToItsSizeRun_AndFallsBackToUnisex()
    {
        using var host = new CatalogHost(postgres);
        var girls = FreshGender(host);
        var unisex = FreshGender(host);
        host.Lookups.Genders.Remove(3);
        host.Lookups.Genders[unisex] = new(unisex, "UNISEX", "Unisex");
        var style = await host.NewStyleAsync(sizes: [4, 5]);
        var product = await host.NewProductAsync(style, genderId: girls, categoryId: 2);
        Assert.False((await host.SendAsync(new GetChartForProductQuery(product.Id))).Found);

        await host.SendAsync(new CreateSizeChartCommand(2, unisex, "Unisex bottoms", [Line(4), Line(5), Line(6)], "Test Person"));
        var fallback = await host.SendAsync(new GetChartForProductQuery(product.Id));
        await host.SendAsync(new CreateSizeChartCommand(2, girls, "Girls bottoms", [Line(4, hMax: 99)], "Test Person"));
        var own = await host.SendAsync(new GetChartForProductQuery(product.Id));

        Assert.True(fallback.Found);
        Assert.Equal([4, 5], fallback.Lines.Select(l => (int)l.SizeId).ToArray());
        Assert.Equal("Girls bottoms", own.Name);
        Assert.Equal(99m, Assert.Single(own.Lines).HeightMaxCm);
    }

    [Fact]
    [Trait("Spec", "CHT-8")]
    public async Task TheList_IsPaged_Ordered_AndConstantInQueries()
    {
        var counter = new CommandCounter();
        using var host = new CatalogHost(postgres, counter: counter);
        for (var i = 0; i < 3; i++)
        {
            await host.SendAsync(new CreateSizeChartCommand(1, FreshGender(host), "Listed", [Line(4), Line(5)], "Test Person"));
        }

        counter.Reset();
        var page = await host.SendAsync(new ListSizeChartsQuery(1, 2));
        var queries = counter.Count;
        var clamped = await host.SendAsync(new ListSizeChartsQuery(1, 9999));

        Assert.Equal(2, page.Items.Count);
        Assert.True(page.TotalCount >= 3);
        Assert.Equal(2, queries);
        Assert.Equal(100, clamped.PageSize);
        Assert.All(page.Items, i => Assert.True(i.LineCount >= 1));
        Assert.Equal(
            page.Items.OrderBy(i => i.CategoryId).ThenBy(i => i.GenderId).ThenBy(i => i.Id).Select(i => i.Id),
            page.Items.Select(i => i.Id));
    }

    [Fact]
    [Trait("Spec", "CHT-3")]
    public async Task APublishWithoutAChart_IsAllowed_AndReportsNoSizeChart()
    {
        using var host = new CatalogHost(postgres);
        var ready = await host.MakePublishableAsync(await host.NewProductAsync(), (1, 4));
        Assert.Contains("NO_SIZE_CHART", ready.Gate.Warnings);

        var result = await host.SendAsync(new PublishProductCommand(ready.Id, ready.Version, "Test Person"));

        Assert.True(result.Published);
    }
}
