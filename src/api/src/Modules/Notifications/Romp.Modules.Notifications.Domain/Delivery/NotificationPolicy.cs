namespace Romp.Modules.Notifications.Domain.Delivery;

/// <summary>
/// How long each kind of notification stays useful, and which goes first when the queue backs up (DLV-10, DLV-11, Q-DLV-3 approved 2026-11-02).
/// A message sent after its time would mislead the customer (a COD request three days late), so the worker records "Expired" instead.
/// </summary>
public static class NotificationPolicy
{
    public static TimeSpan ValidFor(NotificationKind kind) => kind switch
    {
        NotificationKind.OrderConfirmation => TimeSpan.FromDays(7),
        NotificationKind.OrderCancelled => TimeSpan.FromHours(24),
        NotificationKind.CodConfirmationRequest => TimeSpan.FromHours(24),
        NotificationKind.OrderDelayed => TimeSpan.FromHours(48),
        NotificationKind.OrderShipped => TimeSpan.FromDays(3),
        NotificationKind.DeliveryAttemptFailed => TimeSpan.FromHours(24),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Lower is sooner. Confirmations and cancellation notices before reminders (DLV-11).</summary>
    public static int PriorityOf(NotificationKind kind) => kind switch
    {
        NotificationKind.OrderConfirmation => 0,
        NotificationKind.OrderCancelled => 0,
        NotificationKind.CodConfirmationRequest => 1,
        NotificationKind.OrderDelayed => 0,
        NotificationKind.OrderShipped => 1,
        NotificationKind.DeliveryAttemptFailed => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
