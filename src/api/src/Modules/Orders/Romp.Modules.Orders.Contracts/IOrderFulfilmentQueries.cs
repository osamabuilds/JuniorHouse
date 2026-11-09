namespace Romp.Modules.Orders.Contracts;

/// <summary>One line to ship, as Orders recorded it at placement. <c>VariantId</c> is Labelling's variant id, as in the stock events.</summary>
public sealed record OrderFulfilmentLine(long VariantId, string? Sku, string Name, string Colour, string Size, int Quantity, decimal UnitPrice, decimal LineTotal);

/// <summary>
/// What Fulfilment may read about an order (ADR 0026, ADR 0030). It is read at the moment of need (creating the fulfilment order, printing the
/// slip) and never put in an event. Only the recipient's name and city leave Orders: no phone, email or street address (SEC-25, SLP-7).
/// <see cref="CashToCollect"/> is the order total for cash on delivery and zero for any order already paid online.
/// </summary>
public sealed record OrderFulfilmentView(
    long OrderId,
    string OrderNumber,
    string PlacementKey,
    string PaymentMethodCode,
    bool IsCashOnDelivery,
    string StatusCode,
    string RecipientName,
    string City,
    decimal Subtotal,
    decimal Discount,
    decimal Shipping,
    decimal Total,
    decimal CashToCollect,
    DateTimeOffset PlacedAt,
    IReadOnlyList<OrderFulfilmentLine> Lines);

public interface IOrderFulfilmentQueries
{
    /// <summary>The order's lines, totals, payment method and delivery name and city, or null when there is no such order.</summary>
    Task<OrderFulfilmentView?> FindAsync(long orderId, CancellationToken cancellationToken);
}
