namespace Romp.Modules.Orders.Contracts;

/// <summary>
/// What Shipping needs to book a parcel and print its label (ADR 0031): where it goes, who to call, and the cash to collect. It is read at the moment
/// of need (booking, label) and never put in an event or stored by Shipping (ADR 0026, SEC-25).
/// <see cref="CashToCollect"/> is the order total for cash on delivery and zero for an order already paid online.
/// </summary>
public sealed record OrderShippingView(
    long OrderId,
    string OrderNumber,
    string StatusCode,
    string RecipientName,
    string RecipientPhone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string Province,
    string? PostalCode,
    string? DeliveryNote,
    bool IsCashOnDelivery,
    decimal CashToCollect);

public interface IOrderShippingQueries
{
    /// <summary>The order's delivery details, or null when there is no such order.</summary>
    Task<OrderShippingView?> FindAsync(long orderId, CancellationToken cancellationToken);
}
