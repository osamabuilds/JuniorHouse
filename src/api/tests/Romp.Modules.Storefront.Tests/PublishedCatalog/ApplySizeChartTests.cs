using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog task 12: <see cref="ApplySizeChartCommand"/> against a real PostgreSQL - version-wins, removal, and the one-chart-per-category-and-gender slot.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class ApplySizeChartTests(StorefrontPostgres postgres)
{
    private static PublishedSizeChartLine Line(short sizeId, string code, int minMonths) =>
        new(new PublishedCode(sizeId, code, code), minMonths, minMonths + 23, 100m, 115m, 16m, 22m, 58m, 54m, 50m);

    private static PublishedSizeChart Chart(long id, long version, string gender = "GIRLS", string name = "Dresses chart", params PublishedSizeChartLine[] lines) =>
        new(id, version, new PublishedCode(2, "DRESSES", "Dresses"), new PublishedCode(1, gender, gender), name,
            lines.Length > 0 ? lines : [Line(9, "5-6Y", 60), Line(4, "7-8Y", 84)]);

    private static ApplySizeChartCommand Apply(PublishedSizeChart chart) => new(chart.ChartId, chart);

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task Apply_NewChart_StoresItWithItsLines_InSizeOrder()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        Assert.Equal(ProjectionOutcome.Created, await host.SendAsync(Apply(Chart(1, 1))));

        await host.QueryAsync(async db =>
        {
            var chart = await db.Charts.AsNoTracking().SingleAsync();
            Assert.Equal("DRESSES", chart.CategoryCode);
            var lines = await db.ChartLines.AsNoTracking().OrderBy(l => l.SizeSeq).ToListAsync();
            Assert.Equal(["5-6Y", "7-8Y"], lines.Select(l => l.SizeCode));
            Assert.Equal(60, lines[0].AgeMinMonths);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Apply_SameOrOlderChart_ChangesNothing()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(Apply(Chart(1, 5, name: "Current")));

        Assert.Equal(ProjectionOutcome.Unchanged, await host.SendAsync(Apply(Chart(1, 5, name: "Repeat"))));
        Assert.Equal(ProjectionOutcome.Unchanged, await host.SendAsync(Apply(Chart(1, 4, name: "Stale"))));

        Assert.Equal("Current", (await host.QueryAsync(db => db.Charts.AsNoTracking().SingleAsync())).Name);
    }

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task Apply_NewerChart_ReplacesItsLines()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(Apply(Chart(1, 1)));

        Assert.Equal(ProjectionOutcome.Updated, await host.SendAsync(Apply(Chart(1, 2, lines: Line(4, "7-8Y", 84)))));

        await host.QueryAsync(async db =>
        {
            Assert.Equal(["7-8Y"], await db.ChartLines.Select(l => l.SizeCode).ToListAsync());
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Apply_ChartRecreatedForTheSameSlot_ReplacesTheOldOne_WhicheverArrivesFirst()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(Apply(Chart(1, 1)));

        await host.SendAsync(Apply(Chart(2, 7)));
        Assert.Equal([2L], await host.QueryAsync(db => db.Charts.Select(c => c.Id).ToListAsync()));

        // the old chart's own event now arrives late: it is stale and must not come back
        Assert.Equal(ProjectionOutcome.Unchanged, await host.SendAsync(Apply(Chart(1, 1))));
        Assert.Equal([2L], await host.QueryAsync(db => db.Charts.Select(c => c.Id).ToListAsync()));
    }

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task Apply_NullChart_RemovesItAndItsLines_AndUnknownIsAbsent()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(Apply(Chart(1, 1)));

        Assert.Equal(ProjectionOutcome.Removed, await host.SendAsync(new ApplySizeChartCommand(1, null)));
        Assert.Equal(ProjectionOutcome.Absent, await host.SendAsync(new ApplySizeChartCommand(1, null)));

        Assert.Equal(0, await host.QueryAsync(db => db.ChartLines.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Apply_ChartWithoutAVersion_IsRefused()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.SendAsync(Apply(Chart(1, 0))));
    }
}
