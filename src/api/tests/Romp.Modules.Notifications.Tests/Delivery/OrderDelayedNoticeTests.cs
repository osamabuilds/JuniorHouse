using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Fulfilment.Contracts;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Tests.Support;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.Delivery;

/// <summary>S14 F13: a delayed line of a confirmed order tells the customer, once, with ids and codes only in the event (PEX-3, Q-FUL-5).</summary>
public sealed class OrderDelayedNoticeTests(NotificationsPostgres postgres) : IClassFixture<NotificationsPostgres>
{
    private sealed class FakeOrders : IOrderNotificationQueries
    {
        public Task<OrderNotificationView?> FindAsync(long orderId, CancellationToken cancellationToken) =>
            Task.FromResult<OrderNotificationView?>(new OrderNotificationView(orderId, "RMP-7001", "COD", "A", "a@example.com", "+923001234567", true, 100m, []));
    }

    private static long _next = 70_000;

    private static Task HandleAsync(NtfyDbContextHolder holder, OrderLineDelayedEvent evt, long messageId)
    {
        var guard = new InboxGuard(new OutboxMessageContext { MessageId = messageId, Schema = "NTFY" }, TimeProvider.System);
        var create = new CreateNotification(holder.Db, new FakeOrders(), new SuppressionList(holder.Db, new WhatsAppSettings()), TimeProvider.System);
        return new OrderLineDelayedNotificationConsumer(holder.Db, guard, create).HandleAsync(evt, CancellationToken.None);
    }

    private sealed class NtfyDbContextHolder(Romp.Modules.Notifications.Infrastructure.Persistence.NtfyDbContext db)
    {
        public Romp.Modules.Notifications.Infrastructure.Persistence.NtfyDbContext Db { get; } = db;
    }

    [Fact]
    [Trait("Spec", "PEX-3")]
    public async Task ADelayedLine_CreatesOneOrderDelayedNotification_ForTheOrder()
    {
        var orderId = Interlocked.Increment(ref _next);
        await using var db = postgres.NewContext();

        await HandleAsync(new NtfyDbContextHolder(db), new OrderLineDelayedEvent(orderId, "RMP-7001", 12, "SHORT"), 701);

        await using var read = postgres.NewContext();
        var notification = Assert.Single(await read.Notifications.AsNoTracking().Include(n => n.Deliveries).Where(n => n.ReferenceId == orderId).ToArrayAsync());
        Assert.Equal(NotificationKind.OrderDelayed, notification.Kind);
        Assert.Contains(notification.Deliveries, d => d.Channel == NotificationChannel.Email && d.Status == DeliveryStatus.Queued);
        Assert.Equal(DateTimeOffset.UtcNow + TimeSpan.FromHours(48), notification.ExpiresAt, TimeSpan.FromMinutes(1));
    }

    [Fact]
    [Trait("Spec", "PEX-3")]
    public async Task TheSameEventTwice_NotifiesOnce()
    {
        var orderId = Interlocked.Increment(ref _next);
        await using var db = postgres.NewContext();
        var evt = new OrderLineDelayedEvent(orderId, "RMP-7001", 12, "NOT_FOUND");

        await HandleAsync(new NtfyDbContextHolder(db), evt, 702);
        await using var again = postgres.NewContext();
        await HandleAsync(new NtfyDbContextHolder(again), evt, 702);

        await using var read = postgres.NewContext();
        Assert.Single(await read.Notifications.AsNoTracking().Where(n => n.ReferenceId == orderId).ToArrayAsync());
    }

    [Fact]
    [Trait("Spec", "PEX-3")]
    public void TheMessage_SaysPartOfTheOrderIsDelayed_WithoutAnyPersonalData()
    {
        var order = new OrderNotificationView(1, "RMP-7001", "COD", "A", "a@example.com", "+923001234567", true, 100m, []);

        var message = PlainMessageRenderer.RenderPlain(NotificationKind.OrderDelayed, NotificationChannel.Email, order);

        Assert.Contains("delayed", message.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RMP-7001", message.Subject);
        Assert.Equal("ORDL", NotificationCodes.Of(NotificationKind.OrderDelayed));
    }

    [Fact]
    [Trait("Spec", "PEX-3")]
    public void TheDelayedEvent_CarriesIdsAndCodesOnly()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new OrderLineDelayedEvent(5, "RMP-7001", 12, "SHORT"));

        Assert.DoesNotContain("Name", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Mobile", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Email", json, StringComparison.Ordinal);
    }
}
