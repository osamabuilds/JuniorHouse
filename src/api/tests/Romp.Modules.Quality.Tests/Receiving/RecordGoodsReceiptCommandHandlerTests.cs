using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Quality.Application.Receiving;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Tests.Support;

namespace Romp.Modules.Quality.Tests.Receiving;

public sealed class RecordGoodsReceiptCommandHandlerTests
{
    private static readonly DateOnly Today = TestHost.Today;

    private static RecordGoodsReceiptCommand Command(
        long deliveryId, int line1 = 100, int line2 = 50, string person = "Sara Khan", DateOnly? received = null) => new(
        deliveryId, received ?? Today, person, "counted at the dock",
        [new CountedLineDto(1, 10, line1), new CountedLineDto(2, 10, line2)]);

    /// <summary>Collects the in-process notification so the tests can prove it is raised (D's handler opens the batch from it).</summary>
    private sealed class NotificationSpy : INotificationHandler<GoodsReceiptRecordedNotification>
    {
        public static readonly List<long> Seen = [];

        public Task Handle(GoodsReceiptRecordedNotification notification, CancellationToken cancellationToken)
        {
            lock (Seen)
            {
                Seen.Add(notification.GrnId);
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task Handle_ShortCount_RecordedAndFlagged()
    {
        using var host = new TestHost(nameof(Handle_ShortCount_RecordedAndFlagged));
        var deliveryId = await host.AddDeliveryAsync();

        var grn = await host.SendAsync(Command(deliveryId, line1: 90));

        Assert.Equal("Short", grn.ReconciliationCode);
        Assert.Equal(150, grn.ClaimedTotal);
        Assert.Equal(140, grn.CountedTotal);
        Assert.Equal(-10, grn.DiffTotal);
        Assert.Equal(-10, grn.Lines.Single(l => l.SizeId == 1).DiffQty);
        Assert.Equal("GRN-2026-00001", grn.GrnNo);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task Handle_Over_FlagsExcessLines()
    {
        using var host = new TestHost(nameof(Handle_Over_FlagsExcessLines));
        var deliveryId = await host.AddDeliveryAsync();

        var grn = await host.SendAsync(Command(deliveryId, line1: 100, line2: 58));

        Assert.Equal("Over", grn.ReconciliationCode);
        var line = grn.Lines.Single(l => l.SizeId == 2);
        Assert.True(line.IsExcess);
        Assert.Equal(8, line.DiffQty);
        Assert.False(grn.Lines.Single(l => l.SizeId == 1).IsExcess);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task Handle_WritesEventAndNotificationInOneTransaction()
    {
        NotificationSpy.Seen.Clear();
        using var host = new TestHost(nameof(Handle_WritesEventAndNotificationInOneTransaction),
            services => services.AddScoped<INotificationHandler<GoodsReceiptRecordedNotification>, NotificationSpy>());
        var deliveryId = await host.AddDeliveryAsync();

        var grn = await host.SendAsync(Command(deliveryId, line1: 95));

        Assert.Equal(grn.Id, Assert.Single(NotificationSpy.Seen));
        var message = await host.WithDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.EventType == "GoodsReceiptRecordedEvent"));
        using var payload = JsonDocument.Parse(message.Payload);
        Assert.Equal(grn.Id, payload.RootElement.GetProperty("GrnId").GetInt64());
        Assert.Equal(grn.GrnNo, payload.RootElement.GetProperty("GrnNo").GetString());
        Assert.Equal("Short", payload.RootElement.GetProperty("ReconciliationCode").GetString());
        Assert.Equal(grn.Id, message.AggregateId);
        Assert.Equal("GoodsReceipt", message.AggregateType);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    [Trait("Spec", "NFR-FT-08")]
    public async Task Handle_NoConsumerRunning_StillRecordedWithPendingOutboxRow()
    {
        using var host = new TestHost(nameof(Handle_NoConsumerRunning_StillRecordedWithPendingOutboxRow));
        var deliveryId = await host.AddDeliveryAsync();

        await host.SendAsync(Command(deliveryId));

        Assert.Equal(1, await host.WithDbAsync(db => db.GoodsReceipts.CountAsync()));
        var message = await host.WithDbAsync(db => db.Set<OutboxMessage>().AsNoTracking().SingleAsync(m => m.EventType == "GoodsReceiptRecordedEvent"));
        Assert.Null(message.ProcessedDte);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task Handle_SecondGrnForTheSameDelivery_Refused()
    {
        using var host = new TestHost(nameof(Handle_SecondGrnForTheSameDelivery_Refused));
        var deliveryId = await host.AddDeliveryAsync();
        await host.SendAsync(Command(deliveryId));

        var error = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Command(deliveryId)));

        Assert.Contains("already has a goods receipt", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, await host.WithDbAsync(db => db.GoodsReceipts.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task Handle_UnknownDelivery_NotFound()
    {
        using var host = new TestHost(nameof(Handle_UnknownDelivery_NotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(Command(9999)));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task Handle_PoCancelledInVendor_Refused()
    {
        using var host = new TestHost(nameof(Handle_PoCancelledInVendor_Refused));
        var deliveryId = await host.AddDeliveryAsync();
        host.PoQueries.Terms = FakePurchaseOrderQueries.DefaultTerms(statusId: 4);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Command(deliveryId)));

        Assert.Equal(0, await host.WithDbAsync(db => db.GoodsReceipts.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task Handle_LineOnPoButNotOnDelivery_Accepted()
    {
        using var host = new TestHost(nameof(Handle_LineOnPoButNotOnDelivery_Accepted));
        var deliveryId = await host.AddDeliveryAsync();

        var grn = await host.SendAsync(new RecordGoodsReceiptCommand(
            deliveryId, Today, "Sara Khan", null,
            [new CountedLineDto(1, 10, 100), new CountedLineDto(2, 10, 50), new CountedLineDto(3, 10, 7)]));

        Assert.Equal(7, grn.Lines.Single(l => l.SizeId == 3).DiffQty);
        Assert.Equal("Over", grn.ReconciliationCode);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task Handle_InvalidLine_RefusedNamingTheLine()
    {
        using var host = new TestHost(nameof(Handle_InvalidLine_RefusedNamingTheLine));
        var deliveryId = await host.AddDeliveryAsync();

        var error = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new RecordGoodsReceiptCommand(
            deliveryId, Today, "Sara Khan", null, [new CountedLineDto(1, 10, 100), new CountedLineDto(9, 99, 1)])));

        Assert.Contains("size 9", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    [Trait("Spec", "AC-30")]
    public async Task Handle_ValidationProblems_ListedPerField()
    {
        using var host = new TestHost(nameof(Handle_ValidationProblems_ListedPerField));
        var deliveryId = await host.AddDeliveryAsync();

        var error = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new RecordGoodsReceiptCommand(
            deliveryId, Today, "A", null, [new CountedLineDto(1, 10, -5)])));

        Assert.Contains(error.Errors.Keys, key => key.Contains("ActingPerson", StringComparison.Ordinal));
        Assert.Contains(error.Errors.Keys, key => key.Contains("Qty", StringComparison.Ordinal));
        Assert.Equal(0, await host.WithDbAsync(db => db.GoodsReceipts.CountAsync()));
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task Handle_ReceiptBeforeDispatch_Refused_AndFutureRefused()
    {
        using var host = new TestHost(nameof(Handle_ReceiptBeforeDispatch_Refused_AndFutureRefused));
        var deliveryId = await host.AddDeliveryAsync(dispatch: new DateOnly(2026, 9, 25));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Command(deliveryId, received: new DateOnly(2026, 9, 24))));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(Command(deliveryId, received: Today.AddDays(1))));
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task Handle_SeveralDeliveries_EachGetsItsOwnGrn()
    {
        using var host = new TestHost(nameof(Handle_SeveralDeliveries_EachGetsItsOwnGrn));
        var first = await host.AddDeliveryAsync("DN-1");
        var second = await host.AddDeliveryAsync("DN-2");

        var grn1 = await host.SendAsync(Command(first));
        var grn2 = await host.SendAsync(Command(second, line1: 60));

        Assert.NotEqual(grn1.GrnNo, grn2.GrnNo);
        var history = await host.SendAsync(new ListGoodsReceiptsByPoQuery(FakePurchaseOrderQueries.PoId));
        Assert.Equal(["DN-1", "DN-2"], history.Select(h => h.VendorDnNo).ToArray());
        Assert.Equal([150, 110], history.Select(h => h.CountedTotal).ToArray());
    }

    [Fact]
    [Trait("Spec", "AC-45")]
    public void Dtos_NeverCarryCostOrInternalNoteFields()
    {
        var names = new[] { typeof(GoodsReceiptDto), typeof(GrnSummaryDto), typeof(GrnLineDto) }
            .SelectMany(t => t.GetProperties().Select(p => p.Name))
            .ToList();

        Assert.DoesNotContain(names, n => n.Contains("Cost", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Internal", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Price", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Spec", "AC-45")]
    public async Task Handle_NeverExposesInternalPoNotes()
    {
        using var host = new TestHost(nameof(Handle_NeverExposesInternalPoNotes));
        var deliveryId = await host.AddDeliveryAsync();
        var grn = await host.SendAsync(Command(deliveryId));

        var loaded = await host.SendAsync(new GetGoodsReceiptQuery(grn.Id));

        var json = JsonSerializer.Serialize(loaded);
        Assert.DoesNotContain("UnitCost", json, StringComparison.Ordinal);
        Assert.Equal("counted at the dock", loaded.Note); // the receiver's own note, not a PO note
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Handle_UnknownGoodsReceipt_NotFound()
    {
        using var host = new TestHost(nameof(Handle_UnknownGoodsReceipt_NotFound));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new GetGoodsReceiptQuery(4242)));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task Handle_StatusLookupsMatchTheStoredIds()
    {
        using var host = new TestHost(nameof(Handle_StatusLookupsMatchTheStoredIds));
        var deliveryId = await host.AddDeliveryAsync();
        var grn = await host.SendAsync(Command(deliveryId, line1: 99));

        var stored = await host.WithDbAsync(db => db.GoodsReceipts.AsNoTracking().SingleAsync(g => g.Id == grn.Id));

        Assert.Equal(GrnStatus.Recorded, stored.StatusId);
        Assert.Equal(GrnReconciliation.Shortage, stored.ReconciliationId);
    }
}
