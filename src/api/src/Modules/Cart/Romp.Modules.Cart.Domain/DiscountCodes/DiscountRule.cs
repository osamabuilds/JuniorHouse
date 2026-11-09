using Romp.Modules.Cart.Domain.Totals;

namespace Romp.Modules.Cart.Domain.DiscountCodes;

/// <summary>One basket line as a discount sees it: the server's price, how many, which category, and whether it is already on sale.</summary>
public sealed record DiscountLine(decimal UnitPrice, int Quantity, short CategoryId, bool IsOnSale);

/// <summary>Why a code was not accepted. <see cref="Unknown"/> covers unknown, mistyped and deactivated codes alike, so codes cannot be enumerated (DSC-9).</summary>
public enum DiscountRejection
{
    None = 0,
    Unknown,
    Expired,
    Exhausted,
    AlreadyUsed,
    BelowMinimum,
    NoEligibleLines,
    SecondCode,
}

/// <summary>The outcome of trying a code on a basket: the money off, or the reason it is refused. <see cref="MinimumSpend"/> is set for <see cref="DiscountRejection.BelowMinimum"/>.</summary>
public sealed record DiscountResult(decimal Amount, DiscountRejection Rejection, decimal? MinimumSpend = null)
{
    public bool IsAccepted => Rejection == DiscountRejection.None;

    public static DiscountResult Accepted(decimal amount) => new(amount, DiscountRejection.None);

    public static DiscountResult Rejected(DiscountRejection reason, decimal? minimumSpend = null) => new(0m, reason, minimumSpend);
}

/// <summary>
/// The one place a code meets a basket (FR-25, FR-26, DSC-1 to DSC-7, DSC-13): pure, so the cart, checkout and the redemption hold all
/// reach the same answer. The checks run in a fixed order so the reason shown is always the same for the same situation.
/// </summary>
public static class DiscountRule
{
    /// <summary>
    /// <paramref name="usedInTotal"/> counts held and confirmed redemptions of the code; <paramref name="usedByCustomer"/> counts the same for
    /// this customer, or is null when the customer is not known yet (a guest before checkout), in which case that check waits for checkout.
    /// </summary>
    public static DiscountResult Evaluate(
        DiscountCode? code, IReadOnlyCollection<DiscountLine> lines, DateTimeOffset now, int usedInTotal, int? usedByCustomer)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (code is null || !code.IsActive)
        {
            return DiscountResult.Rejected(DiscountRejection.Unknown);
        }

        if (code.ExpiresAt is { } expiry && now >= expiry)
        {
            return DiscountResult.Rejected(DiscountRejection.Expired);
        }

        if (code.UsageCap is { } cap && usedInTotal >= cap)
        {
            return DiscountResult.Rejected(DiscountRejection.Exhausted);
        }

        if (code.PerCustomerCap is { } perCustomer && usedByCustomer is { } mine && mine >= perCustomer)
        {
            return DiscountResult.Rejected(DiscountRejection.AlreadyUsed);
        }

        var subtotal = lines.Sum(l => OrderTotals.Round(l.UnitPrice * l.Quantity));
        if (code.MinimumSpend is { } minimum && subtotal < minimum)
        {
            return DiscountResult.Rejected(DiscountRejection.BelowMinimum, minimum);
        }

        var eligible = lines
            .Where(l => code.Scope.Count == 0 || code.Scope.Any(s => s.CategoryId == l.CategoryId))
            .Where(l => code.IncludesSaleItems || !l.IsOnSale)
            .Sum(l => OrderTotals.Round(l.UnitPrice * l.Quantity));
        if (eligible <= 0)
        {
            return DiscountResult.Rejected(DiscountRejection.NoEligibleLines);
        }

        var amount = code.Kind == DiscountKind.Percentage
            ? OrderTotals.Round(eligible * code.Value / 100m)
            : Math.Min(code.Value, eligible);
        return DiscountResult.Accepted(amount);
    }
}
