namespace Romp.Modules.Cart.Domain.Totals;

/// <summary>One priced line as the totals routine sees it: the unit price the server read and the quantity (SEC-09).</summary>
public sealed record TotalLine(decimal UnitPrice, int Quantity);

/// <summary>
/// Every number the shopper pays (TOT-1 to TOT-7). <see cref="ShippingKnown"/> is false until a city is chosen, when the fee is still
/// "calculated at checkout" and the total does not include it. The parts always add up to the total: <c>Total = Subtotal − Discount + Shipping</c>.
/// </summary>
public sealed record OrderAmounts(decimal Subtotal, decimal Discount, decimal Shipping, bool ShippingKnown, decimal Total);

/// <summary>
/// The one pricing routine used by the cart, by checkout and by the order (FR-34, SEC-09): a pure function, no I/O, so it is tested
/// exhaustively and no second copy can drift. Money is <c>decimal</c> rupees, rounded half away from zero to two decimals (Q-TOT-3).
/// </summary>
public static class OrderTotals
{
    /// <summary>
    /// Subtotal is the sum of each line's price times its quantity, each line rounded to two decimals first so the displayed lines add up to
    /// the displayed subtotal (TOT-2). The discount is capped at the subtotal, so a total is never negative (TOT-6). An empty basket is
    /// all zero and is not charged shipping (TOT-4). Shipping is charged once per order, not per item (TOT-1); a null fee means the city is
    /// not known yet.
    /// </summary>
    public static OrderAmounts Calculate(IEnumerable<TotalLine> lines, decimal discount, decimal? shippingFee)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (discount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(discount), "A discount cannot be negative.");
        }

        if (shippingFee is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shippingFee), "A shipping fee cannot be negative.");
        }

        var subtotal = lines.Sum(l => Round(l.UnitPrice * l.Quantity));
        if (subtotal <= 0)
        {
            return new OrderAmounts(0m, 0m, 0m, ShippingKnown: shippingFee is not null, 0m);
        }

        var applied = Math.Min(Round(discount), subtotal);
        var shipping = Round(shippingFee ?? 0m);
        return new OrderAmounts(subtotal, applied, shipping, shippingFee is not null, subtotal - applied + shipping);
    }

    /// <summary>Rounds to two decimals, halves away from zero (Q-TOT-3).</summary>
    public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
