using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Application.Batches;
using Romp.Modules.Quality.Application.Inspections;
using Romp.Modules.Quality.Application.Inspections.Aql;
using Romp.Modules.Quality.Application.Receiving;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;
using Romp.Modules.Quality.Tests.Support;

namespace Romp.Modules.Quality.Tests.Inspections;

/// <summary>
/// The parts of QC batches and inspections that are PostgreSQL behaviour (Testcontainers, CLAUDE.md): the seeded AQL tables,
/// the paged batch list's SQL, optimistic concurrency, and the receipt-and-batch transaction rolling back together.
/// </summary>
public sealed class QcBatchPostgresTests : IAsyncLifetime
{
    private QcPostgres _postgres = null!;

    public async Task InitializeAsync() => _postgres = await QcPostgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task Migrate_AqlPlan_NoGapsOrOverlapsPerLevel()
    {
        await using var context = _postgres.NewContext();

        var samples = await context.AqlSamples.AsNoTracking().ToListAsync();
        var limits = await context.AqlLimits.AsNoTracking().ToListAsync();

        Assert.Equal(AqlSeedData.SampleRows().Count, samples.Count);
        Assert.Equal(AqlSeedData.LimitRows().Count, limits.Count);
        foreach (var level in samples.GroupBy(s => s.InspectionLevelId))
        {
            var ranges = level.OrderBy(s => s.LotMinQty).ToList();
            Assert.Equal(1, ranges[0].LotMinQty);
            for (var i = 1; i < ranges.Count; i++)
            {
                Assert.Equal(ranges[i - 1].LotMaxQty + 1, ranges[i].LotMinQty);
            }
        }
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public async Task Read_SeededTables_MatchesFixture()
    {
        await using var context = _postgres.NewContext();
        var fromDatabase = await AqlPlanLoader.LoadAsync(context, CancellationToken.None);
        var fixture = new AqlPlanTable(AqlSeedData.SampleRows(), AqlSeedData.LimitRows());

        foreach (var lot in new[] { 51, 200, 1000, 3200, 3201, 100000, 700000 })
        {
            var expected = AqlCalculator.Plan(fixture, lot, InspectionLevel.II, AqlLevel.Aql250, 50);
            var actual = AqlCalculator.Plan(fromDatabase, lot, InspectionLevel.II, AqlLevel.Aql250, 50);
            Assert.Equal(expected, actual);
        }
    }

    private static async Task SeedBatchesAsync(TestHost host, int count, int startAt = 1)
    {
        for (var i = startAt; i < startAt + count; i++)
        {
            await host.RecordGrnAsync($"DN-{i}", line1: 100, line2: 50);
        }
    }

    [Fact]
    [Trait("Spec", "AC-42")]
    public async Task Handle_TwoPages_OrderedWithTotal()
    {
        using var host = new TestHost("pg", postgres: _postgres.ConnectionString);
        await SeedBatchesAsync(host, 7);
        await using var context = _postgres.NewContext();
        var handler = new SearchQcBatchesQueryHandler(context);

        var first = await handler.Handle(new SearchQcBatchesQuery(Page: 1, PageSize: 3), CancellationToken.None);
        var second = await handler.Handle(new SearchQcBatchesQuery(Page: 2, PageSize: 3), CancellationToken.None);
        var third = await handler.Handle(new SearchQcBatchesQuery(Page: 3, PageSize: 3), CancellationToken.None);

        Assert.Equal(7, first.TotalCount);
        var ids = first.Items.Concat(second.Items).Concat(third.Items).Select(r => r.Id).ToList();
        Assert.Equal(7, ids.Distinct().Count());
        Assert.Equal(ids.OrderByDescending(id => id), ids);
        Assert.All(first.Items, b => Assert.StartsWith("GRN-", b.GrnNo, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "AC-42")]
    public async Task Handle_QueryCount_DoesNotGrowWithRows()
    {
        using var host = new TestHost("pg", postgres: _postgres.ConnectionString);
        await SeedBatchesAsync(host, 2);
        var counter = new CommandCounter();
        await using (var small = _postgres.NewContext(counter))
        {
            counter.Reset();
            await new SearchQcBatchesQueryHandler(small).Handle(new SearchQcBatchesQuery(PageSize: 25), CancellationToken.None);
        }

        var withTwo = counter.Count;
        await SeedBatchesAsync(host, 30, startAt: 100);
        await using (var large = _postgres.NewContext(counter))
        {
            counter.Reset();
            var page = await new SearchQcBatchesQueryHandler(large).Handle(new SearchQcBatchesQuery(PageSize: 25), CancellationToken.None);
            Assert.Equal(25, page.Items.Count);
        }

        Assert.Equal(2, withTwo);
        Assert.Equal(withTwo, counter.Count);
    }

    [Fact]
    [Trait("Spec", "AC-42")]
    public async Task Handle_StatusFilterAndSearch()
    {
        using var host = new TestHost("pg", postgres: _postgres.ConnectionString);
        await SeedBatchesAsync(host, 3);
        await using var context = _postgres.NewContext();
        var handler = new SearchQcBatchesQueryHandler(context);

        var pending = await handler.Handle(new SearchQcBatchesQuery(StatusId: BatchStatus.Pending), CancellationToken.None);
        var decided = await handler.Handle(new SearchQcBatchesQuery(StatusId: BatchStatus.Decided), CancellationToken.None);
        var byDn = await handler.Handle(new SearchQcBatchesQuery(Search: "DN-2"), CancellationToken.None);

        Assert.Equal(3, pending.TotalCount);
        Assert.Empty(decided.Items);
        Assert.Equal("DN-2", Assert.Single(byDn.Items).VendorDnNo);
    }

    [Fact]
    [Trait("Spec", "AC-43")]
    public async Task Handle_ConcurrentWrites_SecondConflicts()
    {
        using var host = new TestHost("pg", postgres: _postgres.ConnectionString);
        var (_, batchId) = await host.RecordGrnAsync(line1: 600, line2: 400);
        AqlPlanTable table;
        await using (var planContext = _postgres.NewContext())
        {
            table = await AqlPlanLoader.LoadAsync(planContext, CancellationToken.None);
        }

        var plan = AqlCalculator.Plan(table, 1000, InspectionLevel.II, AqlLevel.Aql250, 50);
        var input = new InspectionInput(
            null, new DateOnly(2026, 9, 29), "Ali Raza", null, InspectionLevel.II, AqlLevel.Aql250,
            CheckType.All.Select(t => new CheckInput(t, CheckResult.Pass)).ToList(), []);

        // Both inspectors open the batch while it is still Pending, then each records and saves.
        await using var first = _postgres.NewContext();
        await using var second = _postgres.NewContext();
        var firstBatch = await first.Batches.Include(b => b.Lines).Include(b => b.SubBatches).Include(b => b.Inspections).SingleAsync(b => b.Id == batchId);
        var secondBatch = await second.Batches.Include(b => b.Lines).Include(b => b.SubBatches).Include(b => b.Inspections).SingleAsync(b => b.Id == batchId);
        firstBatch.RecordInspection(input, plan, 0, new DateOnly(2026, 9, 29));
        secondBatch.RecordInspection(input, plan, 0, new DateOnly(2026, 9, 29));

        await first.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => second.SaveChangesAsync()); // unique round or stale row version: either way ConcurrencyExceptionHandler answers 409

        await using var check = _postgres.NewContext();
        Assert.Equal(1, await check.Inspections.CountAsync());
    }

    /// <summary>A notification handler that fails, standing in for any problem while the QC batch is being opened.</summary>
    private sealed class FailingBatchOpener : INotificationHandler<GoodsReceiptRecordedNotification>
    {
        public Task Handle(GoodsReceiptRecordedNotification notification, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("batch could not be opened");
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    [Trait("Spec", "AC-12")]
    public async Task Handle_BatchOpenFails_ReceiptEventAndBatchAllRollBackTogether()
    {
        using var host = new TestHost(
            "pg", services => services.AddScoped<INotificationHandler<GoodsReceiptRecordedNotification>, FailingBatchOpener>(),
            postgres: _postgres.ConnectionString);
        var deliveryId = await host.AddDeliveryAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.SendAsync(new RecordGoodsReceiptCommand(
            deliveryId, TestHost.Today, "Sara Khan", null, [new CountedLineDto(1, 10, 100), new CountedLineDto(2, 10, 50)])));

        await using var check = _postgres.NewContext();
        Assert.Equal(0, await check.GoodsReceipts.CountAsync());
        Assert.Equal(0, await check.Batches.CountAsync());
        Assert.Equal(0, await check.Set<OutboxMessage>().CountAsync(m => m.EventType == "GoodsReceiptRecordedEvent"));
    }
}
