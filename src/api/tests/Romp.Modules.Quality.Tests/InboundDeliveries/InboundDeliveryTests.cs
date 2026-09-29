using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Quality.Application.InboundDeliveries;
using Romp.Modules.Quality.Domain.InboundDeliveries;
using Romp.Modules.Quality.Infrastructure.Persistence;
using Romp.Modules.Quality.Tests.Support;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Quality.Tests.InboundDeliveries;

public sealed class RecordInboundDeliveryCommandHandlerTests
{
    private static RecordInboundDeliveryCommand Command(string dnNo = "DN-1") => new(
        RunId: 5, PoId: 10, PoNo: "PO-2026-00010", VendorDnNo: dnNo, new DateOnly(2026, 9, 20), IsFinalShipment: false,
        [new ClaimedLine(1, 10, 100), new ClaimedLine(2, 10, 50)]);

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task Handle_StoresClaimedLines()
    {
        using var host = new TestHost(nameof(Handle_StoresClaimedLines));

        var id = await host.SendAsync(Command());

        var stored = await host.WithDbAsync(db => db.Deliveries.AsNoTracking().Include(d => d.Lines).SingleAsync(d => d.Id == id));
        Assert.Equal("DN-1", stored.VendorDnNo);
        Assert.Equal(150, stored.Lines.Sum(l => l.ClaimedQty));
        Assert.Equal(2, stored.Lines.Count);
        Assert.False(stored.IsCancelled);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Handle_SameRunAndDnTwice_OneRecord()
    {
        using var host = new TestHost(nameof(Handle_SameRunAndDnTwice_OneRecord));

        var first = await host.SendAsync(Command());
        var second = await host.SendAsync(Command());

        Assert.Equal(first, second);
        Assert.Equal(1, await host.WithDbAsync(db => db.Deliveries.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Handle_DifferentDnNumbers_TwoRecords()
    {
        using var host = new TestHost(nameof(Handle_DifferentDnNumbers_TwoRecords));

        await host.SendAsync(Command("DN-1"));
        await host.SendAsync(Command("DN-2"));

        Assert.Equal(2, await host.WithDbAsync(db => db.Deliveries.CountAsync()));
    }
}

public sealed class InboundDeliveryConsumerTests
{
    private static TestHost NewHost(string name) => new(name, services =>
    {
        // Mirrors QualityModule's registrations of the consumers (they are internal to Infrastructure).
        var types = typeof(QcDbContext).Assembly.GetTypes();
        services.AddScoped(typeof(IOutboxMessageHandler<DeliveryNoteRecordedEvent>), types.Single(t => t.Name == "DeliveryNoteRecordedConsumer"));
        services.AddScoped(typeof(IOutboxMessageHandler<PoCancelledEvent>), types.Single(t => t.Name == "PoCancelledInboundConsumer"));
    });

    private static Task<bool> InScopeAsync(TestHost host, long messageId, Func<IServiceProvider, Task> action) =>
        host.WithScopeAsync(async sp =>
        {
            var context = sp.GetRequiredService<OutboxMessageContext>();
            context.MessageId = messageId;
            context.Schema = "PROD";
            await action(sp);
            return true;
        });

    private static DeliveryNoteRecordedEvent DeliveryEvent(string dnNo = "DN-9") => new(
        RunId: 5, PoId: 10, PoNo: "PO-2026-00010", VendorDnNo: dnNo, new DateOnly(2026, 9, 20), IsFinalShipment: true,
        PoOutcome: "Delivered", TimingCode: "OnTime", DaysLate: 0, QuantityCode: "OnQuantity", OverQty: 0, ShortQty: 0,
        RevisionNumber: 0, RompFabricDelay: false, Lines: [new DeliveredLine(1, 10, 40), new DeliveredLine(2, 10, 20)]);

    private static Task<bool> DeliverAsync(TestHost host, long messageId, DeliveryNoteRecordedEvent domainEvent) =>
        InScopeAsync(host, messageId, sp => sp.GetServices<IOutboxMessageHandler<DeliveryNoteRecordedEvent>>()
            .Single(h => h.GetType().Name == "DeliveryNoteRecordedConsumer").HandleAsync(domainEvent, CancellationToken.None));

    [Fact]
    [Trait("Spec", "AC-1")]
    [Trait("Spec", "AC-2")]
    public async Task DeliveryNoteRecorded_TwiceSameMessage_OneRecordAndOneInboxRow()
    {
        using var host = NewHost(nameof(DeliveryNoteRecorded_TwiceSameMessage_OneRecordAndOneInboxRow));

        await DeliverAsync(host, 71, DeliveryEvent());
        await DeliverAsync(host, 71, DeliveryEvent());

        var delivery = await host.WithDbAsync(db => db.Deliveries.AsNoTracking().Include(d => d.Lines).SingleAsync());
        Assert.Equal(60, delivery.Lines.Sum(l => l.ClaimedQty));
        Assert.True(delivery.IsFinalShipment);
        var inbox = Assert.Single(await host.WithDbAsync(db => db.Set<InboxMessage>().ToListAsync()));
        Assert.Equal(71, inbox.MessageId);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task DeliveryNoteRecorded_DifferentMessagesSameDn_StillOneRecord()
    {
        using var host = NewHost(nameof(DeliveryNoteRecorded_DifferentMessagesSameDn_StillOneRecord));

        await DeliverAsync(host, 1, DeliveryEvent());
        await DeliverAsync(host, 2, DeliveryEvent());

        Assert.Equal(1, await host.WithDbAsync(db => db.Deliveries.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task PoCancelled_MarksDeliveriesCancelled_AndARedeliveryIsANoOp()
    {
        using var host = NewHost(nameof(PoCancelled_MarksDeliveriesCancelled_AndARedeliveryIsANoOp));
        await DeliverAsync(host, 1, DeliveryEvent("DN-1"));
        await DeliverAsync(host, 2, DeliveryEvent("DN-2"));

        for (var i = 0; i < 2; i++)
        {
            await InScopeAsync(host, 50, sp => sp.GetServices<IOutboxMessageHandler<PoCancelledEvent>>()
                .Single(h => h.GetType().Name == "PoCancelledInboundConsumer")
                .HandleAsync(new PoCancelledEvent(10, "PO-2026-00010", CancelReasonId: 1), CancellationToken.None));
        }

        Assert.All(await host.WithDbAsync(db => db.Deliveries.AsNoTracking().ToListAsync()), d => Assert.True(d.IsCancelled));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task PoCancelled_OtherPo_LeavesDeliveriesOpen()
    {
        using var host = NewHost(nameof(PoCancelled_OtherPo_LeavesDeliveriesOpen));
        await DeliverAsync(host, 1, DeliveryEvent());

        await InScopeAsync(host, 60, sp => sp.GetServices<IOutboxMessageHandler<PoCancelledEvent>>()
            .Single(h => h.GetType().Name == "PoCancelledInboundConsumer")
            .HandleAsync(new PoCancelledEvent(999, "PO-2026-00999", CancelReasonId: 1), CancellationToken.None));

        Assert.False((await host.WithDbAsync(db => db.Deliveries.AsNoTracking().SingleAsync())).IsCancelled);
    }
}

/// <summary>AC-42 (CONVENTIONS section 4): the receiving queue is one COUNT plus one page query however many deliveries there are. Real PostgreSQL (Testcontainers) because the GRN flag is computed by SQL.</summary>
public sealed class SearchInboundDeliveriesQueryHandlerTests : IAsyncLifetime
{
    private QcPostgres _postgres = null!;

    public async Task InitializeAsync() => _postgres = await QcPostgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private async Task SeedAsync(int count, int startAt = 1)
    {
        await using var context = _postgres.NewContext();
        for (var i = startAt; i < startAt + count; i++)
        {
            context.Deliveries.Add(new InboundDelivery(i, 100 + i, $"PO-2026-{i:D5}", $"DN-{i}", new DateOnly(2026, 9, 20), false, [new ClaimedLine(1, 10, i)]));
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    [Trait("Spec", "AC-42")]
    public async Task Handle_TwoPages_OrderedWithTotal()
    {
        await SeedAsync(7);
        await using var context = _postgres.NewContext();
        var handler = new SearchInboundDeliveriesQueryHandler(context);

        var first = await handler.Handle(new SearchInboundDeliveriesQuery(Page: 1, PageSize: 3), CancellationToken.None);
        var second = await handler.Handle(new SearchInboundDeliveriesQuery(Page: 2, PageSize: 3), CancellationToken.None);
        var third = await handler.Handle(new SearchInboundDeliveriesQuery(Page: 3, PageSize: 3), CancellationToken.None);

        Assert.Equal(7, first.TotalCount);
        var ids = first.Items.Concat(second.Items).Concat(third.Items).Select(r => r.Id).ToList();
        Assert.Equal(7, ids.Distinct().Count());
        Assert.Equal(ids.OrderByDescending(id => id), ids);
        Assert.Equal(7, first.Items[0].ClaimedTotal); // newest delivery claims 7 pieces
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
            await new SearchInboundDeliveriesQueryHandler(small).Handle(new SearchInboundDeliveriesQuery(PageSize: 25), CancellationToken.None);
        }

        var withTwo = counter.Count;
        await SeedAsync(30, startAt: 100);
        await using (var large = _postgres.NewContext(counter))
        {
            counter.Reset();
            var page = await new SearchInboundDeliveriesQueryHandler(large).Handle(new SearchInboundDeliveriesQuery(PageSize: 25), CancellationToken.None);
            Assert.Equal(25, page.Items.Count);
        }

        Assert.Equal(2, withTwo);
        Assert.Equal(withTwo, counter.Count);
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task Handle_StatusFilterAndSearch()
    {
        await SeedAsync(4);
        await using (var context = _postgres.NewContext())
        {
            (await context.Deliveries.SingleAsync(d => d.RunId == 4)).MarkCancelled();
            await context.SaveChangesAsync();
        }

        await using var read = _postgres.NewContext();
        var handler = new SearchInboundDeliveriesQueryHandler(read);

        var open = await handler.Handle(new SearchInboundDeliveriesQuery(Status: "open"), CancellationToken.None);
        var cancelled = await handler.Handle(new SearchInboundDeliveriesQuery(Status: "cancelled"), CancellationToken.None);
        var byNo = await handler.Handle(new SearchInboundDeliveriesQuery(Search: "00002"), CancellationToken.None);
        var byDn = await handler.Handle(new SearchInboundDeliveriesQuery(Search: "DN-3"), CancellationToken.None);

        Assert.Equal(3, open.TotalCount);
        Assert.Equal("DN-4", Assert.Single(cancelled.Items).VendorDnNo);
        Assert.Equal("PO-2026-00002", Assert.Single(byNo.Items).PoNo);
        Assert.Equal("DN-3", Assert.Single(byDn.Items).VendorDnNo);
    }
}
