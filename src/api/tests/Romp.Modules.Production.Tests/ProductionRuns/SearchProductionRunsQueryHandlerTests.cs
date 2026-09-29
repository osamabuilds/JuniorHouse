using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Application.ProductionRuns;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Infrastructure.Persistence;
using Romp.Modules.Production.Tests.Support;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Production.Tests.ProductionRuns;

/// <summary>
/// AC-32 (CONVENTIONS section 4): the list is one COUNT plus one page query however many runs there are, ordered stably.
/// Real PostgreSQL (Testcontainers) because the at-risk flag and the shipped total are computed by SQL.
/// </summary>
public sealed class SearchProductionRunsQueryHandlerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly CommandCounter _counter = new();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private ProductionDbContext NewContext(bool counting = false)
    {
        var builder = new DbContextOptionsBuilder<ProductionDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "PROD"));
        if (counting)
        {
            builder.AddInterceptors(_counter);
        }

        return new ProductionDbContext(builder.Options);
    }

    private async Task SeedAsync(int runs)
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();

        for (var i = 1; i <= runs; i++)
        {
            var run = new ProductionRun(1000 + i, $"PO-2026-{i:D5}", 1, Runs.Sent, new DateOnly(2026, 10, 25));
            run.SubmitPpSample(new DateOnly(2026, 9, 5), Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays);
            run.DecidePpSample(1, true, Runs.Person, null, Runs.Today, DateTimeOffset.UtcNow);
            run.RecordMilestone(MilestoneType.BulkCuttingStarted, new DateOnly(2026, 9, 10), Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays);
            run.SetExpectedCompletion(i % 2 == 0 ? new DateOnly(2026, 10, 24) : new DateOnly(2026, 10, 1), null, Runs.Person); // even runs are at risk
            run.RecordDeliveryNote($"DN-{i}", new DateOnly(2026, 9, 25), false, [new NoteLineInput(1, 10, i)], Runs.Terms(), Runs.Person, Runs.Today);
            context.Runs.Add(run);
        }

        await context.SaveChangesAsync();
    }

    private static SearchProductionRunsQueryHandler Handler(ProductionDbContext context) => new(context, new ProductionOptions());

    [Fact]
    [Trait("Spec", "AC-32")]
    public async Task Handle_TwoPages_OrderedWithTotal()
    {
        await SeedAsync(7);
        await using var context = NewContext();

        var first = await Handler(context).Handle(new SearchProductionRunsQuery(Page: 1, PageSize: 3), CancellationToken.None);
        var second = await Handler(context).Handle(new SearchProductionRunsQuery(Page: 2, PageSize: 3), CancellationToken.None);
        var third = await Handler(context).Handle(new SearchProductionRunsQuery(Page: 3, PageSize: 3), CancellationToken.None);

        Assert.Equal(7, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        var ids = first.Items.Concat(second.Items).Concat(third.Items).Select(r => r.Id).ToList();
        Assert.Equal(7, ids.Count);
        Assert.Equal(7, ids.Distinct().Count()); // pages never overlap
        Assert.Equal(ids.OrderByDescending(id => id), ids); // stable newest-first order across pages
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public async Task Handle_PageSize_IsClamped()
    {
        await SeedAsync(3);
        await using var context = NewContext();

        var huge = await Handler(context).Handle(new SearchProductionRunsQuery(Page: 0, PageSize: 5000), CancellationToken.None);

        Assert.Equal(1, huge.Page);
        Assert.Equal(100, huge.PageSize);
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public async Task Handle_QueryCount_DoesNotGrowWithRuns()
    {
        await SeedAsync(2);
        await using (var small = NewContext(counting: true))
        {
            _counter.Reset();
            await Handler(small).Handle(new SearchProductionRunsQuery(PageSize: 25), CancellationToken.None);
        }

        var withTwo = _counter.Count;

        await SeedMoreAsync(30);
        await using (var large = NewContext(counting: true))
        {
            _counter.Reset();
            var page = await Handler(large).Handle(new SearchProductionRunsQuery(PageSize: 25), CancellationToken.None);
            Assert.Equal(25, page.Items.Count);
        }

        Assert.Equal(2, withTwo); // one COUNT and one page
        Assert.Equal(withTwo, _counter.Count);
    }

    private async Task SeedMoreAsync(int extra)
    {
        await using var context = NewContext();
        for (var i = 100; i < 100 + extra; i++)
        {
            context.Runs.Add(new ProductionRun(5000 + i, $"PO-2026-{i:D5}", 1, Runs.Sent, new DateOnly(2026, 10, 25)));
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public async Task Handle_AtRiskFilter_ReturnsOnlyRunsExpectedAfterTheBuffer_AndFlagsThem()
    {
        await SeedAsync(6);
        await using var context = NewContext();

        var atRisk = await Handler(context).Handle(new SearchProductionRunsQuery(AtRisk: true), CancellationToken.None);
        var all = await Handler(context).Handle(new SearchProductionRunsQuery(), CancellationToken.None);

        Assert.Equal(3, atRisk.TotalCount);
        Assert.All(atRisk.Items, r => Assert.True(r.IsAtRisk));
        Assert.Equal(3, all.Items.Count(r => !r.IsAtRisk));
    }

    [Fact]
    [Trait("Spec", "AC-19")]
    public async Task Handle_ReportsShippedTotalAndFlagsPerRun()
    {
        await SeedAsync(3);
        await using var context = NewContext();

        var page = await Handler(context).Handle(new SearchProductionRunsQuery(), CancellationToken.None);

        Assert.Equal([3, 2, 1], page.Items.Select(r => r.ShippedQty).ToArray()); // run i shipped i pieces
        Assert.All(page.Items, r =>
        {
            Assert.True(r.HasApprovedSample);
            Assert.True(r.IsCuttingStarted);
            Assert.False(r.HasOverDelivery);
        });
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public async Task Handle_FilterByPoNoAndStatus()
    {
        await SeedAsync(4);
        await using var context = NewContext();

        var byNo = await Handler(context).Handle(new SearchProductionRunsQuery(PoNo: "00003"), CancellationToken.None);
        var byStatus = await Handler(context).Handle(new SearchProductionRunsQuery(StatusId: RunStatus.Cancelled), CancellationToken.None);

        Assert.Equal("PO-2026-00003", Assert.Single(byNo.Items).PoNo);
        Assert.Empty(byStatus.Items);
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => _count;

        public void Reset() => _count = 0;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
