using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Application.Delivery;

/// <summary>What one Orders event asks Notifications to send: the kind, the order, and the event it came from.</summary>
public sealed record NotificationIntent(NotificationKind Kind, long OrderId, Guid EventId);

/// <summary>
/// The one place Orders' events become Notifications' own words (anti-corruption mapper, ADR 0026). Returns null when the event needs no
/// message: a card order is confirmed only after payment, not at placement (OCF-3).
/// </summary>
public static class OrderEventMapper
{
    private const string CashOnDeliveryCode = "COD";

    public static NotificationIntent? Map(OrderPlacedEvent placed) =>
        placed.PaymentMethodCode == CashOnDeliveryCode
            ? new NotificationIntent(NotificationKind.OrderConfirmation, placed.OrderId, placed.EventId)
            : null;

    public static NotificationIntent Map(OrderPaidEvent paid) =>
        new(NotificationKind.OrderConfirmation, paid.OrderId, paid.EventId);

    public static NotificationIntent Map(OrderCancelledEvent cancelled) =>
        new(NotificationKind.OrderCancelled, cancelled.OrderId, cancelled.EventId);

    public static NotificationIntent Map(CodConfirmationRequestedEvent requested) =>
        new(NotificationKind.CodConfirmationRequest, requested.OrderId, requested.EventId);
}
