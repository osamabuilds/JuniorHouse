using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Shipping.Contracts;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.Delivery;

/// <summary>S15 N1: a collected parcel and a failed delivery attempt tell the customer, once, with the track link (TRK-7, OHS-7).</summary>
public sealed class ShipmentNoticeTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>
{
    private sealed class FakeOrders : IOrderNotificationQueries
    {
        public Task<OrderNotificationView?> FindAsync(long orderId, CancellationToken cancellationToken) =>
            Task.FromResult<OrderNotificationView?>(new OrderNotificationView(orderId, "RMP-7001", "COD", "A", "a@example.com", "+923001234567", true, 100m, []));
    }

    private static long _next = 70_000;

    private static Task HandleAsync(NtfyDbContextHolder holder, ShipmentStatusChangedEvent evt, long messageId)
    {
        var guard = new InboxGuard(new OutboxMessageContext { MessageId = messageId, Schema = "NTFY" }, TimeProvider.System);
        var create = new CreateNotification(holder.Db, new FakeOrders(), new SuppressionList(holder.Db, new WhatsAppSettings()), TimeProvider.System);
        return new ShipmentStatusNotificationConsumer(holder.Db, guard, create).HandleAsync(evt, CancellationToken.None);
    }

    private sealed class NtfyDbContextHolder(Romp.Modules.Notifications.Infrastructure.Persistence.NtfyDbContext db)
    {
        public Romp.Modules.Notifications.Infrastructure.Persistence.NtfyDbContext Db { get; } = db;
    }

    [Theory]
    [Trait("Spec", "TRK-7")]
    [InlineData(ShipmentStatusCodes.FailedAttempt, NotificationKind.DeliveryAttemptFailed)]
    [InlineData(ShipmentStatusCodes.PickedUp, NotificationKind.OrderShipped)]
    public async Task AFailedAttemptAndAPickUp_EachCreateOneNotification_OfTheRightKind(string status, NotificationKind kind)
    {
        var orderId = Interlocked.Increment(ref _next);
        await using var db = postgres.NewContext();

        await HandleAsync(new NtfyDbContextHolder(db), new ShipmentStatusChangedEvent(orderId, "RMP-7001", status, "PRM-1"), 801 + (int)kind);

        await using var read = postgres.NewContext();
        var notification = Assert.Single(await read.Notifications.AsNoTracking().Include(n => n.Deliveries).Where(n => n.ReferenceId == orderId).ToArrayAsync());
        Assert.Equal(kind, notification.Kind);
        Assert.Contains(notification.Deliveries, d => d.Channel == NotificationChannel.Email && d.Status == DeliveryStatus.Queued);
    }

    [Theory]
    [Trait("Spec", "TRK-7")]
    [InlineData(ShipmentStatusCodes.Booked)]
    [InlineData(ShipmentStatusCodes.InTransit)]
    [InlineData(ShipmentStatusCodes.OutForDelivery)]
    [InlineData(ShipmentStatusCodes.Delivered)]
    [InlineData(ShipmentStatusCodes.Cancelled)]
    public async Task OtherStatuses_SendNothing(string status)
    {
        var orderId = Interlocked.Increment(ref _next);
        await using var db = postgres.NewContext();

        await HandleAsync(new NtfyDbContextHolder(db), new ShipmentStatusChangedEvent(orderId, "RMP-7001", status, "PRM-1"), 820);

        await using var read = postgres.NewContext();
        Assert.Empty(await read.Notifications.AsNoTracking().Where(n => n.ReferenceId == orderId).ToArrayAsync());
    }

    [Fact]
    [Trait("Spec", "TRK-7")]
    public async Task TheSameEventTwice_NotifiesOnce()
    {
        var orderId = Interlocked.Increment(ref _next);
        var evt = new ShipmentStatusChangedEvent(orderId, "RMP-7001", ShipmentStatusCodes.FailedAttempt, "PRM-1");
        await using var db = postgres.NewContext();

        await HandleAsync(new NtfyDbContextHolder(db), evt, 830);
        await using var again = postgres.NewContext();
        await HandleAsync(new NtfyDbContextHolder(again), evt, 830);

        await using var read = postgres.NewContext();
        Assert.Single(await read.Notifications.AsNoTracking().Where(n => n.ReferenceId == orderId).ToArrayAsync());
    }

    [Fact]
    [Trait("Spec", "OHS-7")]
    public void TheDispatchMessage_CarriesTheSignedTrackLink_AndTheFailureMessageTellsWhatToDo()
    {
        var order = new OrderNotificationView(1, "RMP-7001", "COD", "A", "a@example.com", "+923001234567", true, 100m, [], TrackUrl: "https://shop.example/track-order?token=abc");

        var shipped = PlainMessageRenderer.RenderPlain(NotificationKind.OrderShipped, NotificationChannel.Email, order);
        var failed = PlainMessageRenderer.RenderPlain(NotificationKind.DeliveryAttemptFailed, NotificationChannel.Email, order);

        Assert.Contains("on its way", shipped.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://shop.example/track-order?token=abc", shipped.Text);
        Assert.Contains("could not deliver", failed.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("phone is on", failed.Text);
        Assert.Equal(("ORSH", "DAFL"), (NotificationCodes.Of(NotificationKind.OrderShipped), NotificationCodes.Of(NotificationKind.DeliveryAttemptFailed)));
    }

    [Fact]
    [Trait("Spec", "ADR-0011")]
    public void TheStatusEvent_CarriesIdsCodesAndTheWaybillOnly()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new ShipmentStatusChangedEvent(5, "RMP-7001", "FAILED_ATTEMPT", "PRM-1"));

        Assert.DoesNotContain("Mobile", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Email", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Address", json, StringComparison.Ordinal);
    }
}
