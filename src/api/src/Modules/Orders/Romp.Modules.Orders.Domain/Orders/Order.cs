using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Orders.Domain.Orders;

/// <summary>What asking an order to change state did. A repeat is <see cref="Unchanged"/> and harmless (ORD-5).</summary>
public enum OrderTransition
{
    Changed,
    Unchanged,

    /// <summary>The state does not allow it; <see cref="Order.Refusal"/> style messages say why.</summary>
    NotAllowed,
}

/// <summary>
/// The only allowed moves between order states (Q-ORD-4), tested exhaustively. Cancelling a dispatched or delivered order is refused and
/// points to returns (ORD-10). A cash on delivery order that is confirmed can be cancelled until it is dispatched.
/// </summary>
public static class OrderTransitions
{
    private static readonly Dictionary<short, short[]> Allowed = new Dictionary<short, short[]>
    {
        [OrderStatuses.PendingConfirmation] = [OrderStatuses.Confirmed, OrderStatuses.Cancelled],
        [OrderStatuses.PaymentPending] = [OrderStatuses.Paid, OrderStatuses.Cancelled],
        [OrderStatuses.Confirmed] = [OrderStatuses.Cancelled, OrderStatuses.Dispatched],
        [OrderStatuses.Paid] = [OrderStatuses.Cancelled, OrderStatuses.Dispatched],
        [OrderStatuses.Dispatched] = [OrderStatuses.Delivered],
        [OrderStatuses.Delivered] = [],
        [OrderStatuses.Cancelled] = [],
    };

    public static bool IsAllowed(short from, short to) => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static IReadOnlyList<short> From(short from) => Allowed.TryGetValue(from, out var targets) ? targets : [];
}

/// <summary>The contact and delivery details taken at placement and never rewritten (snapshots, so a later profile change cannot alter a placed order).</summary>
public sealed record OrderContact(string Name, string? Email, string Mobile, bool WhatsAppOptIn);

public sealed record OrderAddress(
    string RecipientName, string RecipientPhone, string Line1, string? Line2, short CityId, string CityName, short ProvinceId, string ProvinceName,
    string? PostalCode, string? Note);

/// <summary>A line as priced by the server at placement.</summary>
public sealed record OrderLineData(long VariantId, long ProductId, string? Sku, string Name, string Colour, string Size, decimal UnitPrice, int Quantity);

/// <summary>One placed line (ORDR.ORDR_LINE): a snapshot of what was bought and at what price.</summary>
public sealed class OrderLine : Entity<long>, IAuditable
{
    private OrderLine()
    {
        Name = string.Empty;
        Colour = string.Empty;
        Size = string.Empty;
        InsrBy = string.Empty;
    }

    internal OrderLine(OrderLineData data)
    {
        VariantId = data.VariantId;
        ProductId = data.ProductId;
        Sku = data.Sku;
        Name = data.Name;
        Colour = data.Colour;
        Size = data.Size;
        UnitPrice = data.UnitPrice;
        Quantity = data.Quantity;
        InsrBy = string.Empty;
    }

    public long OrderId { get; private set; }

    public long VariantId { get; private set; }

    public long ProductId { get; private set; }

    public string? Sku { get; private set; }

    public string Name { get; private set; }

    public string Colour { get; private set; }

    public string Size { get; private set; }

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => decimal.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>One step in an order life (ORDR.ORDR_HIST): append-only, with who did it. The database forbids updates and deletes.</summary>
public sealed class OrderHistory : Entity<long>, IAuditable
{
    public const int MaxNoteLength = 200;

    private OrderHistory()
    {
        InsrBy = string.Empty;
    }

    internal OrderHistory(long orderId, short? fromStatusId, short toStatusId, short? reasonId, short actorKindId, string? actorRef, string? note, DateTimeOffset at)
    {
        OrderId = orderId;
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;
        ReasonId = reasonId;
        ActorKindId = actorKindId;
        ActorRef = actorRef;
        Note = note is { Length: > MaxNoteLength } ? note[..MaxNoteLength] : note;
        At = at;
        InsrBy = string.Empty;
    }

    public long OrderId { get; private set; }

    public short? FromStatusId { get; private set; }

    public short ToStatusId { get; private set; }

    public short? ReasonId { get; private set; }

    public short ActorKindId { get; private set; }

    /// <summary>A staff account id, or null for the customer, the system and the gateway.</summary>
    public string? ActorRef { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset At { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Everything placing an order needs, already priced by the server and validated by the application layer.</summary>
public sealed record PlaceOrderData(
    string OrderNumber,
    string PlacementKey,
    long? CustomerId,
    OrderContact Contact,
    OrderAddress Address,
    IReadOnlyList<OrderLineData> Lines,
    decimal Discount,
    decimal Shipping,
    string? DiscountCode,
    short PaymentMethodId,
    string? CartRef,
    DateTimeOffset PlacedAt);

/// <summary>
/// A placed order (ORDR.ORDR_MAIN). Contact, address and lines are snapshots (ORD-6). Totals are checked here against the lines, so an order
/// can never be saved whose amounts do not add up (SEC-09). Status moves only along <see cref="OrderTransitions"/> and every move writes a
/// history row (ORD-5).
/// </summary>
public sealed class Order : AggregateRoot<long>, IAuditable
{
    public const int MaxLines = 100;

    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderHistory> _history = [];

    private Order()
    {
        OrderNumber = string.Empty;
        PlacementKey = string.Empty;
        ContactName = string.Empty;
        ContactMobile = string.Empty;
        RecipientName = string.Empty;
        RecipientPhone = string.Empty;
        AddressLine1 = string.Empty;
        CityName = string.Empty;
        ProvinceName = string.Empty;
        InsrBy = string.Empty;
    }

    public string OrderNumber { get; private set; }

    public string PlacementKey { get; private set; }

    public long? CustomerId { get; private set; }

    public short StatusId { get; private set; }

    public short PaymentMethodId { get; private set; }

    public string? CartRef { get; private set; }

    public string ContactName { get; private set; }

    public string? ContactEmail { get; private set; }

    public string ContactMobile { get; private set; }

    public bool WhatsAppOptIn { get; private set; }

    public DateTimeOffset? WhatsAppOptInAt { get; private set; }

    public string RecipientName { get; private set; }

    public string RecipientPhone { get; private set; }

    public string AddressLine1 { get; private set; }

    public string? AddressLine2 { get; private set; }

    public short CityId { get; private set; }

    public string CityName { get; private set; }

    public short ProvinceId { get; private set; }

    public string ProvinceName { get; private set; }

    public string? PostalCode { get; private set; }

    public string? DeliveryNote { get; private set; }

    public decimal Subtotal { get; private set; }

    public decimal Discount { get; private set; }

    public decimal Shipping { get; private set; }

    public decimal Total { get; private set; }

    public string? DiscountCode { get; private set; }

    public DateTimeOffset PlacedAt { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines;

    public IReadOnlyList<OrderHistory> History => _history;

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Creates the order in its first state: Pending Confirmation for cash on delivery, Payment Pending for an online method.</summary>
    public static Order Place(PlaceOrderData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!OrderNumbers.IsValid(data.OrderNumber))
        {
            throw new DomainException("The order number is not valid.");
        }

        if (string.IsNullOrWhiteSpace(data.PlacementKey) || data.PlacementKey.Length > 64)
        {
            throw new DomainException("An order needs a placement key of up to 64 characters.");
        }

        if (!PaymentMethods.IsKnown(data.PaymentMethodId))
        {
            throw new DomainException("Choose a payment method.");
        }

        if (data.Lines.Count is 0 or > MaxLines)
        {
            throw new DomainException($"An order has between 1 and {MaxLines} lines.");
        }

        if (data.Lines.Any(l => l.Quantity <= 0 || l.UnitPrice <= 0 || l.VariantId <= 0))
        {
            throw new DomainException("Every order line needs a variant, a price and a quantity.");
        }

        if (data.Discount < 0 || data.Shipping < 0)
        {
            throw new DomainException("A discount or delivery amount cannot be negative.");
        }

        var lines = data.Lines.Select(l => new OrderLine(l)).ToList();
        var subtotal = lines.Sum(l => l.LineTotal);
        if (data.Discount > subtotal)
        {
            throw new DomainException("The discount cannot be more than the subtotal.");
        }

        var order = new Order
        {
            OrderNumber = data.OrderNumber,
            PlacementKey = data.PlacementKey,
            CustomerId = data.CustomerId,
            PaymentMethodId = data.PaymentMethodId,
            CartRef = data.CartRef,
            ContactName = data.Contact.Name,
            ContactEmail = data.Contact.Email,
            ContactMobile = data.Contact.Mobile,
            WhatsAppOptIn = data.Contact.WhatsAppOptIn,
            WhatsAppOptInAt = data.Contact.WhatsAppOptIn ? data.PlacedAt : null,
            RecipientName = data.Address.RecipientName,
            RecipientPhone = data.Address.RecipientPhone,
            AddressLine1 = data.Address.Line1,
            AddressLine2 = data.Address.Line2,
            CityId = data.Address.CityId,
            CityName = data.Address.CityName,
            ProvinceId = data.Address.ProvinceId,
            ProvinceName = data.Address.ProvinceName,
            PostalCode = data.Address.PostalCode,
            DeliveryNote = data.Address.Note,
            Subtotal = subtotal,
            Discount = data.Discount,
            Shipping = data.Shipping,
            Total = subtotal - data.Discount + data.Shipping,
            DiscountCode = data.DiscountCode,
            PlacedAt = data.PlacedAt,
            StatusId = PaymentMethods.IsCashOnDelivery(data.PaymentMethodId) ? OrderStatuses.PendingConfirmation : OrderStatuses.PaymentPending,
        };
        order._lines.AddRange(lines);
        return order;
    }

    /// <summary>Writes the first history row and raises <see cref="OrderPlacedEvent"/>. Called once the order has its id (the event carries it).</summary>
    public void MarkPlaced()
    {
        if (_history.Count > 0)
        {
            return;
        }

        _history.Add(new OrderHistory(Id, null, StatusId, null, ActorKinds.Customer, null, null, PlacedAt));
        Raise(new OrderPlacedEvent(Id, OrderNumber, PlacementKey, PaymentMethods.CodeOf(PaymentMethodId), CartRef));
    }

    /// <summary>Moves the order along an allowed step and records it. Repeating the move the order is already in changes nothing.</summary>
    public OrderTransition MoveTo(short to, short actorKindId, string? actorRef, short? reasonId, string? note, DateTimeOffset at)
    {
        if (!OrderStatuses.IsKnown(to) || !ActorKinds.IsKnown(actorKindId))
        {
            throw new DomainException("Unknown order state or actor.");
        }

        if (StatusId == to)
        {
            return OrderTransition.Unchanged;
        }

        if (!OrderTransitions.IsAllowed(StatusId, to))
        {
            return OrderTransition.NotAllowed;
        }

        var from = StatusId;
        StatusId = to;
        _history.Add(new OrderHistory(Id, from, to, reasonId, actorKindId, actorRef, note, at));
        return OrderTransition.Changed;
    }

    /// <summary>
    /// Cancels the order (ORD-9). Refused once it is dispatched or delivered, with a message that points to returns (ORD-10). Raises
    /// <see cref="OrderCancelledEvent"/> so Warehouse and Cart give back what they held.
    /// </summary>
    public OrderTransition Cancel(short reasonId, short actorKindId, string? actorRef, string? note, bool wasPicked, DateTimeOffset at)
    {
        if (!CancelReasons.IsKnown(reasonId))
        {
            throw new DomainException("Choose a cancellation reason.");
        }

        if (StatusId is OrderStatuses.Dispatched or OrderStatuses.Delivered)
        {
            return OrderTransition.NotAllowed;
        }

        var result = MoveTo(OrderStatuses.Cancelled, actorKindId, actorRef, reasonId, note, at);
        if (result == OrderTransition.Changed)
        {
            Raise(new OrderCancelledEvent(Id, OrderNumber, PlacementKey, CancelReasons.CodeOf(reasonId), wasPicked));
        }

        return result;
    }

    public const string CannotCancelAfterDispatch = "This order has already been dispatched, so it cannot be cancelled. Please start a return once it arrives.";
}
