using Microsoft.EntityFrameworkCore;
using Romp.Modules.Quality.Application.Receiving;
using Romp.Modules.Quality.Domain.InboundDeliveries;
using Romp.Modules.Quality.Domain.Receiving;
using Romp.Modules.Quality.Infrastructure.Receiving;
using Romp.Modules.Quality.Tests.Support;

namespace Romp.Modules.Quality.Tests.Receiving;

/// <summary>
/// The parts of goods receipt that are PostgreSQL behaviour, so they run against a real database (Testcontainers,
/// CLAUDE.md): the ADR 0006 number upsert under concurrency, and the paged list's SQL.
/// </summary>
public sealed class GoodsReceiptPostgresTests : IAsyncLifetime
{
    private QcPostgres _postgres = null!;

    public async Task InitializeAsync() => _postgres = await QcPostgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Allocate_Concurrent_NoDuplicates()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));

        var numbers = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
        {
            await using var context = _postgres.NewContext();
            return await new GrnNumberAllocator(context, time).AllocateAsync(CancellationToken.None);
        }));

        Assert.Equal(25, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.Matches(@"^GRN-2026-\d{5}$", n));
        Assert.Equal(Enumerable.Range(1, 25).Select(i => $"GRN-2026-{i:D5}").Order(), numbers.Order());
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Allocate_NewYear_ResetsSequence()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 12, 31, 10, 0, 0, TimeSpan.Zero));
        await using var context = _postgres.NewContext();
        var allocator = new GrnNumberAllocator(context, time);

        var lastOfYear = await allocator.AllocateAsync(CancellationToken.None);
        var secondOfYear = await allocator.AllocateAsync(CancellationToken.None);
        time.Now = new DateTimeOffset(2027, 1, 2, 10, 0, 0, TimeSpan.Zero);
        var firstOfNext = await allocator.AllocateAsync(CancellationToken.None);

        Assert.Equal("GRN-2026-00001", lastOfYear);
        Assert.Equal("GRN-2026-00002", secondOfYear);
        Assert.Equal("GRN-2027-00001", firstOfNext);
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Allocate_UsesPakistanYear_NotUtc()
    {
        // 19:30 UTC on 31 December is already 00:30 on 1 January in Pakistan (+05:00).
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 12, 31, 19, 30, 0, TimeSpan.Zero));
        await using var context = _postgres.NewContext();

        Assert.Equal("GRN-2027-00001", await new GrnNumberAllocator(context, time).AllocateAsync(CancellationToken.None));
    }

    private async Task SeedAsync(int count, int startAt = 1)
    {
        await using var context = _postgres.NewContext();
        var deliveries = Enumerable.Range(startAt, count).Select(i =>
            new InboundDelivery(i, 100 + i % 3, $"PO-2026-{100 + i % 3:D5}", $"DN-{i}", new DateOnly(2026, 9, 20), false, [new ClaimedLine(1, 10, i)])).ToList();
        context.Deliveries.AddRange(deliveries);
        await context.SaveChangesAsync();

        foreach (var delivery in deliveries)
        {
            context.GoodsReceipts.Add(GoodsReceipt.Open(
                delivery, $"GRN-2026-{delivery.RunId:D5}", new DateOnly(2026, 9, 25), "Sara Khan", null,
                [new CountedLine(1, 10, (int)delivery.RunId)], new HashSet<(short, short)>(), new DateOnly(2026, 9, 29), 0));
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    [Trait("Spec", "AC-42")]
    public async Task Handle_TwoPages_OrderedWithTotal()
    {
        await SeedAsync(7);
        await using var context = _postgres.NewContext();
        var handler = new SearchGoodsReceiptsQueryHandler(context);

        var first = await handler.Handle(new SearchGoodsReceiptsQuery(Page: 1, PageSize: 3), CancellationToken.None);
        var second = await handler.Handle(new SearchGoodsReceiptsQuery(Page: 2, PageSize: 3), CancellationToken.None);
        var third = await handler.Handle(new SearchGoodsReceiptsQuery(Page: 3, PageSize: 3), CancellationToken.None);

        Assert.Equal(7, first.TotalCount);
        var ids = first.Items.Concat(second.Items).Concat(third.Items).Select(r => r.Id).ToList();
        Assert.Equal(7, ids.Distinct().Count());
        Assert.Equal(ids.OrderByDescending(id => id), ids);
    }

    [Fact]
    [Trait("Spec", "AC-42")]
    public async Task Handle_QueryCount_DoesNotGrowWithRows()
    {
        await SeedAsync(2);
        var counter = new CommandCounter();
        await using (var small = _postgres.NewContext(counter))
        {
            counter.Reset();
            await new SearchGoodsReceiptsQueryHandler(small).Handle(new SearchGoodsReceiptsQuery(PageSize: 25), CancellationToken.None);
        }

        var withTwo = counter.Count;
        await SeedAsync(30, startAt: 100);
        await using (var large = _postgres.NewContext(counter))
        {
            counter.Reset();
            var page = await new SearchGoodsReceiptsQueryHandler(large).Handle(new SearchGoodsReceiptsQuery(PageSize: 25), CancellationToken.None);
            Assert.Equal(25, page.Items.Count);
        }

        Assert.Equal(2, withTwo);
        Assert.Equal(withTwo, counter.Count);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task ListByPo_SeveralDns_SeveralGrns()
    {
        await SeedAsync(6);
        await using var context = _postgres.NewContext();

        var history = await new ListGoodsReceiptsByPoQueryHandler(context).Handle(new ListGoodsReceiptsByPoQuery(100), CancellationToken.None);

        Assert.Equal(2, history.Count); // runs 3 and 6 belong to PO 100 (i % 3 == 0)
        Assert.All(history, h => Assert.Equal("PO-2026-00100", h.PoNo));
    }

    [Fact]
    [Trait("Spec", "AC-42")]
    public async Task Handle_FilterByReconciliationAndSearch()
    {
        await SeedAsync(4);
        await using var context = _postgres.NewContext();
        var handler = new SearchGoodsReceiptsQueryHandler(context);

        var byNo = await handler.Handle(new SearchGoodsReceiptsQuery(Search: "DN-2"), CancellationToken.None);
        var shortOnes = await handler.Handle(new SearchGoodsReceiptsQuery(ReconciliationId: Romp.Modules.Quality.Domain.GrnReconciliation.Shortage), CancellationToken.None);
        var matched = await handler.Handle(new SearchGoodsReceiptsQuery(ReconciliationId: Romp.Modules.Quality.Domain.GrnReconciliation.Matched), CancellationToken.None);

        Assert.Equal("DN-2", Assert.Single(byNo.Items).VendorDnNo);
        Assert.Empty(shortOnes.Items);
        Assert.Equal(4, matched.TotalCount);
    }
}
