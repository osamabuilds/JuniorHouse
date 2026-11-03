using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Orders.Contracts;

/// <summary>
/// The shared envelope of every Orders event (ADR 0004, ADR 0025). Payloads carry ids, the order number, the placement key and codes only:
/// never a name, address, phone, email or note (ADR 0011, SEC-25). Notifications loads what it needs through
/// <see cref="IOrderNotificationQueries"/> when it sends. Schema changes within a version are additive.
/// </summary>
public abstract record OrdersEvent : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;
    public string AggregateType => "Order";

    public abstract long AggregateId { get; }

    public short SchemaVersion => 1;
}

/// <summary>
/// An order was placed (ORD-1). Warehouse confirms the reservation held under <see cref="PlacementKey"/>, Cart confirms the discount hold
/// and empties the basket named by <see cref="CartRef"/>, and Notifications sends the confirmation for cash on delivery.
/// </summary>
public sealed record OrderPlacedEvent(long OrderId, string OrderNumber, string PlacementKey, string PaymentMethodCode, string? CartRef) : OrdersEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>An online order was paid (OCF-3).</summary>
public sealed record OrderPaidEvent(long OrderId, string OrderNumber) : OrdersEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>
/// An order was cancelled (ORD-9). Warehouse releases the reservation named by <see cref="PlacementKey"/>, Cart releases the discount hold,
/// and Notifications sends the cancellation notice. <see cref="WasPicked"/> is true when stock had already been picked, so a put-back is due (S14).
/// </summary>
public sealed record OrderCancelledEvent(long OrderId, string OrderNumber, string PlacementKey, string ReasonCode, bool WasPicked) : OrdersEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>A cash on delivery order is due a WhatsApp confirmation request: opted in and still unconfirmed after the wait (COD-13).</summary>
public sealed record CodConfirmationRequestedEvent(long OrderId, string OrderNumber, DateTimeOffset DueAt) : OrdersEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>An order was confirmed (cash on delivery confirmed by staff or the customer). Later sprints start fulfilment from it.</summary>
public sealed record OrderConfirmedEvent(long OrderId, string OrderNumber) : OrdersEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>A payment message looked wrong (bad amount or bad signature on a known order). Staff are alerted; nothing about the payer is carried.</summary>
public sealed record OrderPaymentSuspectEvent(long OrderId, string OrderNumber, string ReasonCode) : OrdersEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>
/// What Notifications may read about an order when it sends a message (ADR 0026). Contact details are read here, at send time, and are
/// never put in an event. The mobile is the normalised international form.
/// </summary>
public interface IOrderNotificationQueries
{
    Task<OrderNotificationView?> FindAsync(long orderId, CancellationToken cancellationToken);
}

public sealed record OrderNotificationLine(string Name, string Colour, string Size, int Quantity, decimal LineTotal);

public sealed record OrderNotificationView(
    long OrderId,
    string OrderNumber,
    string PaymentMethodCode,
    string ContactName,
    string? ContactEmail,
    string ContactMobile,
    bool WhatsAppOptIn,
    decimal Total,
    IReadOnlyList<OrderNotificationLine> Lines,
    string? CancelReasonCode = null,
    string? CodConfirmUrl = null);

/// <summary>How a customer's WhatsApp reply to the COD confirmation request ended (COD-14, COD-15, COD-16).</summary>
public enum CodReplyOutcome
{
    Confirmed,
    Cancelled,

    /// <summary>The order was already confirmed, cancelled or has moved on: nothing changed (a repeated delivery lands here).</summary>
    AlreadyHandled,

    /// <summary>No such order, or the number that replied is not the order's mobile: nothing changed.</summary>
    Ignored,
}

/// <summary>
/// Records the customer's tap on Confirm or Cancel in the WhatsApp request (ADR 0026). Notifications has already verified the signature and matched
/// the reply to the message it answers; Orders still checks that the replying number is the order's mobile, and that the order can still be answered.
/// </summary>
public interface ICodCustomerResponses
{
    Task<CodReplyOutcome> RecordReplyAsync(long orderId, string senderMobile, bool confirm, CancellationToken cancellationToken);
}
