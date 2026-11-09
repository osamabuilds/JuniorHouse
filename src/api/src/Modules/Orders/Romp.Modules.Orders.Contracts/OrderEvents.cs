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
    string? CodConfirmUrl = null,
    string? TrackUrl = null);

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

/// <summary>
/// An order's contact in the masked form staff may see in the delivery log (DLG-6, SEC-25): <c>a***@mail.com</c> and <c>03XX-XXX4567</c>.
/// Nothing here can be turned back into the real address or number.
/// </summary>
public sealed record OrderContactSummary(long OrderId, string OrderNumber, string? MaskedEmail, string MaskedMobile);

/// <summary>
/// Lets Notifications show and search recipients without ever holding them (ADR 0026, SEC-25). Both calls are bulk, so a page of the
/// delivery log costs two queries however many rows it has.
/// </summary>
public interface IOrderContactSummaries
{
    /// <summary>The masked contact of each order that exists; unknown ids are simply absent.</summary>
    Task<IReadOnlyDictionary<long, OrderContactSummary>> FindManyAsync(IReadOnlyCollection<long> orderIds, CancellationToken cancellationToken);

    /// <summary>
    /// The ids of orders matching <paramref name="term"/> exactly as an order number, an email address or a mobile number (any usual way of
    /// writing it). Exact on purpose: no wildcard search over personal data. At most <paramref name="max"/> ids.
    /// </summary>
    Task<IReadOnlyList<long>> FindOrderIdsAsync(string term, int max, CancellationToken cancellationToken);
}

/// <summary>Masks contact details for display. Pure, so it is tested without a database.</summary>
public static class ContactMasking
{
    /// <summary><c>ayesha@mail.com</c> becomes <c>a***@mail.com</c>; anything that is not an address becomes <c>***</c>.</summary>
    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var at = email.LastIndexOf('@');
        return at < 1 ? "***" : string.Concat(email.AsSpan(0, 1), "***", email.AsSpan(at));
    }

    /// <summary><c>+923001234567</c> becomes <c>03XX-XXX4567</c>: the network prefix and the last four digits are enough for support to recognise a number.</summary>
    public static string MaskMobile(string? mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("92", StringComparison.Ordinal) && digits.Length == 12)
        {
            digits = "0" + digits[2..];
        }

        return digits.Length == 11
            ? string.Concat(digits.AsSpan(0, 2), "XX-XXX", digits.AsSpan(7))
            : digits.Length >= 4 ? "XXXX" + digits[^4..] : "XXXX";
    }
}
