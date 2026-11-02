namespace Romp.Modules.Cart.Contracts;

/// <summary>One basket line as checkout and the order see it: the server's price, never anything the client sent (SEC-09).</summary>
public sealed record CheckoutLine(
    long VariantId, long ProductId, string Name, string Path, string? Sku, string Colour, string Size, decimal UnitPrice, int Quantity, decimal LineTotal);

/// <summary>
/// The caller's basket priced for checkout. <see cref="CanOrder"/> is false when the basket is empty or any line is unavailable or short of
/// stock, and then <see cref="Problems"/> says so. <see cref="CartRef"/> names the basket so the order event can empty it (ADR 0025).
/// </summary>
public sealed record CheckoutPricing(
    IReadOnlyList<CheckoutLine> Lines,
    decimal Subtotal,
    decimal Discount,
    decimal Shipping,
    bool ShippingKnown,
    decimal Total,
    string? CartRef,
    bool CanOrder,
    IReadOnlyList<string> Problems);

/// <summary>
/// The only way another module (Orders) prices a basket (ADR 0024). It reads the caller's own basket from the session or the guest cookie of
/// the current request, never from an id, and runs the one pricing routine, so every amount on an order comes from here (TOT-3, ORD-4).
/// </summary>
public interface ICheckoutPricing
{
    Task<CheckoutPricing> PriceCurrentCartAsync(int? cityId, bool isCashOnDelivery, CancellationToken cancellationToken);
}
