using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Application.Delivery;

/// <summary>
/// Turns an <see cref="NotificationIntent"/> into a <see cref="Notification"/> with its channel deliveries (DLV-1, DLV-4, DLV-6). It only
/// adds to the context: the caller saves, so the notification commits in the same transaction as the inbox row that records the event.
/// </summary>
public sealed class CreateNotification(INotificationsDbContext dbContext, IOrderNotificationQueries orders, TimeProvider clock)
{
    /// <summary>Returns the notification, or null when this event and kind already produced one (a redelivery creates nothing new).</summary>
    public async Task<Notification?> HandleAsync(NotificationIntent intent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);

        var exists = await dbContext.Notifications
            .AnyAsync(n => n.EventId == intent.EventId && n.Kind == intent.Kind, cancellationToken);
        if (exists)
        {
            return null;
        }

        // Opt-in is read from the order, with its timestamp, never from an event (SEC-25). The order is committed before its event is
        // dispatched, so a missing order is a fault to retry, not a reason to skip the customer.
        var order = await orders.FindAsync(intent.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {intent.OrderId} was not found for a {intent.Kind} notification.");

        var now = clock.GetUtcNow();
        var notification = new Notification(
            intent.Kind, ReferenceKinds.Order, intent.OrderId, intent.EventId, now,
            NotificationPolicy.ValidFor(intent.Kind), NotificationPolicy.PriorityOf(intent.Kind));

        // Suppression (STOP) joins the plan with the channels feature (H7); until then no number is suppressed.
        notification.Plan(ChannelPlan.For(order.WhatsAppOptIn, numberSuppressed: false), now);

        dbContext.Notifications.Add(notification);
        return notification;
    }
}
