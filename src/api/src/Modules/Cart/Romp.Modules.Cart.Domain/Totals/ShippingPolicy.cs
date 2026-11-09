namespace Romp.Modules.Cart.Domain.Totals;

/// <summary>
/// What delivery costs (Q-TOT-1, owner 2026-11-02): a fee per city that staff set in the admin, a default fee for a city with no rate of
/// its own, an optional free-delivery threshold, and an optional extra fee for Cash on Delivery. Pure: no I/O, so it is exhaustively tested.
/// A change applies from the next read; placed orders keep the fee they were placed with (TOT-5).
/// </summary>
public sealed class ShippingPolicy(IReadOnlyDictionary<int, decimal> cityFees, decimal defaultFee, decimal? freeThreshold, decimal? codFee)
{
    public IReadOnlyDictionary<int, decimal> CityFees { get; } = cityFees ?? throw new ArgumentNullException(nameof(cityFees));

    public decimal DefaultFee { get; } = RequireMoney(defaultFee, nameof(defaultFee));

    /// <summary>An order whose subtotal after the discount reaches this amount is delivered free; null means no free-delivery offer.</summary>
    public decimal? FreeThreshold { get; } = freeThreshold is { } t ? RequireMoney(t, nameof(freeThreshold)) : null;

    /// <summary>Added to a Cash on Delivery order; null means COD costs nothing extra.</summary>
    public decimal? CodFee { get; } = codFee is { } c ? RequireMoney(c, nameof(codFee)) : null;

    /// <summary>
    /// The fee for one order, or null while the city is not chosen (shown as "calculated at checkout", TOT-4). The delivery part is the
    /// city's rate or the default, waived when the threshold is reached; the COD fee is added on top for a COD order. The result is rounded to
    /// two decimals and charged once for the whole order, never per item (TOT-1).
    /// </summary>
    public decimal? FeeFor(int? cityId, decimal subtotalAfterDiscount, bool isCashOnDelivery)
    {
        if (cityId is null)
        {
            return null;
        }

        var delivery = CityFees.TryGetValue(cityId.Value, out var own) ? own : DefaultFee;
        if (FreeThreshold is { } threshold && subtotalAfterDiscount >= threshold)
        {
            delivery = 0m;
        }

        return OrderTotals.Round(delivery + (isCashOnDelivery ? CodFee ?? 0m : 0m));
    }

    private static decimal RequireMoney(decimal amount, string name) =>
        amount < 0 ? throw new ArgumentOutOfRangeException(name, "A fee cannot be negative.") : OrderTotals.Round(amount);
}
