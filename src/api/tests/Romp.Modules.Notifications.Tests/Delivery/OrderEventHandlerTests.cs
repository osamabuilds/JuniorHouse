using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Infrastructure.Persistence;
using Romp.Modules.Notifications.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.Delivery;

public sealed class OrderEventHandlerTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>
{
    private sealed class FakeOrders(bool optedIn = true, bool exists = true) : IOrderNotificationQueries
    {
        public Task<OrderNotificationView?> FindAsync(long orderId, CancellationToken cancellationToken) =>
            Task.FromResult<OrderNotificationView?>(exists
                ? new OrderNotificationView(orderId, "RMP-0001", "COD", "A", "a@example.com", "+923001234567", optedIn, 100m, [])
                : null);
    }

    private sealed class Rig(NtfyDbContext db, bool optedIn = true, bool orderExists = true)
    {
        public NtfyDbContext Db { get; } = db;

        public Task Deliver<TEvent>(TEvent evt, long messageId, Func<NtfyDbContext, InboxGuard, CreateNotification, IOutboxMessageHandler<TEvent>> make)
        {
            var guard = new InboxGuard(new OutboxMessageContext { MessageId = messageId, Schema = "NTFY" }, TimeProvider.System);
            var create = new CreateNotification(Db, new FakeOrders(optedIn, orderExists), TimeProvider.System);
            return make(Db, guard, create).HandleAsync(evt, CancellationToken.None);
        }

        public Task Placed(OrderPlacedEvent evt, long messageId) =>
            Deliver(evt, messageId, (d, g, c) => new OrderPlacedNotificationConsumer(d, g, c));

        public Task Paid(OrderPaidEvent evt, long messageId) =>
            Deliver(evt, messageId, (d, g, c) => new OrderPaidNotificationConsumer(d, g, c));

        public Task Cancelled(OrderCancelledEvent evt, long messageId) =>
            Deliver(evt, messageId, (d, g, c) => new OrderCancelledNotificationConsumer(d, g, c));

        public Task CodRequested(CodConfirmationRequestedEvent evt, long messageId) =>
            Deliver(evt, messageId, (d, g, c) => new CodConfirmationRequestedNotificationConsumer(d, g, c));
    }

    private static long NextOrder = 1000;

    private static long NewOrderId() => Interlocked.Increment(ref NextOrder);

    private Task<Notification[]> ForOrder(long orderId)
    {
        var db = postgres.NewContext();
        return db.Notifications.AsNoTracking().Include(n => n.Deliveries).Where(n => n.ReferenceId == orderId).ToArrayAsync();
    }

    [Fact]
    [Trait("Spec", "DLV-1")]
    [Trait("Spec", "OCF-1")]
    public async Task OrderPlaced_CodWithOptIn_CreatesConfirmationWithEmailAndWhatsApp()
    {
        var orderId = NewOrderId();
        await using var db = postgres.NewContext();

        await new Rig(db).Placed(new OrderPlacedEvent(orderId, "RMP-0001", "k1", "COD", null), 1);

        var notification = Assert.Single(await ForOrder(orderId));
        Assert.Equal(NotificationKind.OrderConfirmation, notification.Kind);
        Assert.Equal(ReferenceKinds.Order, notification.ReferenceKind);
        Assert.Contains(notification.Deliveries, d => d.Channel == NotificationChannel.Email && d.Status == DeliveryStatus.Queued);
        Assert.Contains(notification.Deliveries, d => d.Channel == NotificationChannel.WhatsApp && d.Status == DeliveryStatus.Queued);
        Assert.Equal(DateTimeOffset.UtcNow + TimeSpan.FromDays(7), notification.ExpiresAt, TimeSpan.FromMinutes(1));
    }

    [Fact]
    [Trait("Spec", "CHN-1")]
    public async Task OrderPlaced_WithoutOptIn_CreatesEmailAndRecordsWhyWhatsAppWasSkipped()
    {
        var orderId = NewOrderId();
        await using var db = postgres.NewContext();

        await new Rig(db, optedIn: false).Placed(new OrderPlacedEvent(orderId, "RMP-0002", "k2", "COD", null), 2);

        var notification = Assert.Single(await ForOrder(orderId));
        Assert.Contains(notification.Deliveries, d => d.Channel == NotificationChannel.Email && d.Status == DeliveryStatus.Queued);
        Assert.Contains(notification.Deliveries, d => d.Channel == NotificationChannel.WhatsApp && d.Status == DeliveryStatus.Skipped);
    }

    [Fact]
    [Trait("Spec", "OCF-3")]
    public async Task OrderPlaced_Card_CreatesNothingUntilPaid()
    {
        var orderId = NewOrderId();
        await using var db = postgres.NewContext();
        var rig = new Rig(db);

        await rig.Placed(new OrderPlacedEvent(orderId, "RMP-0003", "k3", "CARD", null), 3);
        Assert.Empty(await ForOrder(orderId));

        await rig.Paid(new OrderPaidEvent(orderId, "RMP-0003"), 4);
        var notification = Assert.Single(await ForOrder(orderId));
        Assert.Equal(NotificationKind.OrderConfirmation, notification.Kind);
    }

    [Fact]
    [Trait("Spec", "OCF-4")]
    public async Task OrderCancelled_CreatesCancellationNoticeValidFor24Hours()
    {
        var orderId = NewOrderId();
        await using var db = postgres.NewContext();

        await new Rig(db).Cancelled(new OrderCancelledEvent(orderId, "RMP-0004", "k4", "CNCL", false), 5);

        var notification = Assert.Single(await ForOrder(orderId));
        Assert.Equal(NotificationKind.OrderCancelled, notification.Kind);
        Assert.Equal(DateTimeOffset.UtcNow + TimeSpan.FromHours(24), notification.ExpiresAt, TimeSpan.FromMinutes(1));
    }

    [Fact]
    [Trait("Spec", "COD-13")]
    public async Task CodConfirmationRequested_CreatesLowerPriorityRequest()
    {
        var orderId = NewOrderId();
        await using var db = postgres.NewContext();

        await new Rig(db).CodRequested(new CodConfirmationRequestedEvent(orderId, "RMP-0005", DateTimeOffset.UtcNow), 6);

        var notification = Assert.Single(await ForOrder(orderId));
        Assert.Equal(NotificationKind.CodConfirmationRequest, notification.Kind);
        Assert.Equal(1, notification.Priority);
    }

    [Fact]
    [Trait("Spec", "DLV-4")]
    [Trait("Spec", "DLV-6")]
    [Trait("Spec", "OCF-6")]
    public async Task OrderPlaced_RedeliveredAsTheSameOutboxMessage_CreatesOneNotification()
    {
        var orderId = NewOrderId();
        var evt = new OrderPlacedEvent(orderId, "RMP-0006", "k6", "COD", null);

        await using (var first = postgres.NewContext())
        {
            await new Rig(first).Placed(evt, 7);
        }

        await using (var again = postgres.NewContext())
        {
            await new Rig(again).Placed(evt, 7);
        }

        Assert.Single(await ForOrder(orderId));
    }

    [Fact]
    [Trait("Spec", "DLV-4")]
    public async Task OrderPlaced_SameEventUnderANewOutboxMessage_StillCreatesOneNotification()
    {
        var orderId = NewOrderId();
        var evt = new OrderPlacedEvent(orderId, "RMP-0007", "k7", "COD", null);

        await using (var first = postgres.NewContext())
        {
            await new Rig(first).Placed(evt, 8);
        }

        await using (var again = postgres.NewContext())
        {
            await new Rig(again).Placed(evt, 9);
        }

        Assert.Single(await ForOrder(orderId));
    }

    [Fact]
    [Trait("Spec", "DLV-1")]
    public async Task OrderPlaced_WhenTheOrderCannotBeRead_FailsSoTheOutboxRetriesAndSavesNothing()
    {
        var orderId = NewOrderId();
        await using var db = postgres.NewContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new Rig(db, orderExists: false).Placed(new OrderPlacedEvent(orderId, "RMP-0008", "k8", "COD", null), 10));

        Assert.Empty(await ForOrder(orderId));
    }
}
