using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Application.SizeCharts;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Domain.SizeCharts;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.SizeCharts;

/// <summary>
/// A size chart's content version (CTLG.CHRT_MAIN.CNTT_VER) is the storefront's "highest version wins" key for charts (PCT-5). It comes
/// from one database sequence, so it never repeats: a chart deleted and created again for the same category and gender gets a higher
/// version than the one that was removed, which a per-row counter could not guarantee.
/// </summary>
public sealed class SizeChartVersionDomainTests
{
    private static SizeChart NewChart() =>
        new(3, 2, "Dresses", [new SizeChartLine(4, 12, 24, 80, 92, null, null, null, null, null)]);

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void TheChangedEvent_CarriesTheChartsVersion()
    {
        var chart = NewChart();
        chart.Stamp(7);

        chart.NoteChanged();

        var changed = Assert.IsType<SizeChartChangedEvent>(Assert.Single(chart.DomainEvents));
        Assert.Equal(7, changed.Version);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void AVersionMustGoUp()
    {
        var chart = NewChart();
        chart.Stamp(7);

        Assert.Throws<DomainException>(() => chart.Stamp(7));
        Assert.Throws<DomainException>(() => chart.Stamp(6));
        chart.Stamp(8);
        Assert.Equal(8, chart.ContentVersion);
    }

    [Fact]
    [Trait("Spec", "ADR-0011")]
    public void APayloadWrittenBeforeTheVersionExisted_StillReads_AsVersionZero()
    {
        var e = JsonSerializer.Deserialize<SizeChartChangedEvent>("{\"ChartId\":4,\"CategoryId\":3,\"GenderId\":2}")!;

        Assert.Equal(4, e.AggregateId);
        Assert.Equal(0, e.Version);
    }
}

[Collection(CatalogPostgresFixtureDefinition.Name)]
public sealed class SizeChartVersionDatabaseTests(CatalogPostgres postgres)
{
    private static long _genderSeed = 22_000;

    private static short FreshGender(CatalogHost host)
    {
        var id = (short)Interlocked.Increment(ref _genderSeed);
        host.Lookups.Genders[id] = new(id, "G" + id, "Gender " + id);
        return id;
    }

    private static SizeChartLineInput Line(short size) => new(size, 12, 24, 80, 92, null, null, null, null, null);

    private async Task<long> VersionOfAsync(long chartId)
    {
        await using var db = postgres.NewContext();
        return await db.Charts.AsNoTracking().Where(c => c.Id == chartId).Select(c => c.ContentVersion).SingleAsync();
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task ANewChart_GetsAVersion_AndEveryEditGivesItAHigherOne()
    {
        using var host = new CatalogHost(postgres);
        var chart = await host.SendAsync(new CreateSizeChartCommand(3, FreshGender(host), "Dresses", [Line(4)], "Test Person"));
        var created = await VersionOfAsync(chart.Id);

        var edited = await host.SendAsync(new UpdateSizeChartCommand(chart.Id, "Dresses v2", [Line(4), Line(5)], chart.Version, "Test Person"));
        var afterFirstEdit = await VersionOfAsync(chart.Id);
        await host.SendAsync(new UpdateSizeChartCommand(chart.Id, "Dresses v3", [Line(4)], edited.Version, "Test Person"));
        var afterSecondEdit = await VersionOfAsync(chart.Id);

        Assert.True(created > 0);
        Assert.True(afterFirstEdit > created);
        Assert.True(afterSecondEdit > afterFirstEdit);
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task TwoCharts_NeverShareAVersion()
    {
        using var host = new CatalogHost(postgres);
        var one = await host.SendAsync(new CreateSizeChartCommand(3, FreshGender(host), "One", [Line(4)], "Test Person"));
        var two = await host.SendAsync(new CreateSizeChartCommand(3, FreshGender(host), "Two", [Line(4)], "Test Person"));

        Assert.NotEqual(await VersionOfAsync(one.Id), await VersionOfAsync(two.Id));
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task AChartDeletedAndCreatedAgainForTheSameCategoryAndGender_GetsAHigherVersionThanTheOneRemoved()
    {
        using var host = new CatalogHost(postgres);
        var gender = FreshGender(host);
        var first = await host.SendAsync(new CreateSizeChartCommand(3, gender, "Dresses", [Line(4)], "Test Person"));
        var edited = await host.SendAsync(new UpdateSizeChartCommand(first.Id, "Dresses v2", [Line(4)], first.Version, "Test Person"));
        var removedVersion = await VersionOfAsync(first.Id);
        Assert.Equal(first.Id, edited.Id);

        await host.SendAsync(new DeleteSizeChartCommand(first.Id, "Test Person"));
        var again = await host.SendAsync(new CreateSizeChartCommand(3, gender, "Dresses again", [Line(4)], "Test Person"));

        Assert.True(await VersionOfAsync(again.Id) > removedVersion);
    }
}
