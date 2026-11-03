using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Infrastructure.Persistence;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Tests.Support;

/// <summary>A clock the test moves by hand, so retry schedules and leases are checked without waiting.</summary>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = start;

    public void Advance(TimeSpan by) => Now += by;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class StubOrders(string? email = "a@example.com", string name = "Ayesha") : IOrderNotificationQueries
{
    public Task<OrderNotificationView?> FindAsync(long orderId, CancellationToken cancellationToken) =>
        Task.FromResult<OrderNotificationView?>(new OrderNotificationView(
            orderId, "RMP-0001", "COD", name, email, "+923001234567", false, 100m,
            [new OrderNotificationLine(name, "Red", "4-5Y", 1, 100m)]));
}

/// <summary>An email sender that does what the test script says, one outcome per call, and records what it was asked to send.</summary>
internal sealed class ScriptedSender(params Exception?[] script) : IChannelSender
{
    private int _calls;

    public NotificationChannel Channel => NotificationChannel.Email;

    public List<OutboundMessage> Sent { get; } = [];

    public int Calls => _calls;

    public Task<string?> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        var outcome = _calls < script.Length ? script[_calls] : null;
        _calls++;
        if (outcome is not null)
        {
            throw outcome;
        }

        Sent.Add(message);
        return Task.FromResult<string?>("prov-" + _calls);
    }
}

internal static class DeliveryTestKit
{
    public static DeliveryProcessor Processor(NtfyDbContext db, TestClock clock, IEnumerable<IChannelSender> senders, IOrderNotificationQueries? orders = null, ISuppressionList? suppression = null) =>
        new(db, new SkipLockedDeliveryClaimer(db), orders ?? new StubOrders(), new PlainMessageRenderer(), suppression ?? new SuppressionList(db, new WhatsAppSettings()), senders, new DeliverySettings(), clock);

    /// <summary>Saves <paramref name="count"/> notifications, each with one queued email delivery; returns the delivery ids.</summary>
    public static async Task<long[]> SeedAsync(NtfyDbContext db, int count, DateTimeOffset now, NotificationKind kind = NotificationKind.OrderConfirmation)
    {
        var made = new List<Notification>();
        for (var i = 0; i < count; i++)
        {
            var notification = new Notification(
                kind, ReferenceKinds.Order, 5000 + i, Guid.NewGuid(), now, NotificationPolicy.ValidFor(kind), NotificationPolicy.PriorityOf(kind));
            notification.Plan(new ChannelPlanResult(true, false, null), now);
            db.Notifications.Add(notification);
            made.Add(notification);
        }

        await db.SaveChangesAsync();
        return made.SelectMany(n => n.Deliveries).Select(d => d.Id).ToArray();
    }

    public static Task<ChannelDelivery> LoadAsync(NtfyDbContext db, long id) =>
        db.Set<ChannelDelivery>().AsNoTracking().SingleAsync(d => d.Id == id);
}
