namespace Romp.Modules.Storefront.Domain.PublishedCatalog;

/// <summary>
/// The version-wins rule of the read model (published-catalog plan, PCT-5): a snapshot replaces what is stored only when its version is
/// higher. Repeated, reordered and replayed snapshots therefore cannot regress a row. Versions from the producers are at least 1; a
/// version of 0 means "the sender did not say" and never replaces anything (the caller fetches the real snapshot instead).
/// </summary>
public static class VersionedSnapshot
{
    public static bool Replaces(long incomingVersion, long? storedVersion) =>
        incomingVersion >= 1 && (storedVersion is null || incomingVersion > storedVersion.Value);
}

/// <summary>What a shopper is told about one variant. The exact quantity is never published (Q-PCT-2).</summary>
public enum AvailabilityClass
{
    /// <summary>No stock posting yet, or the variant cannot be sold yet (no live GTIN or no price): shown as "not yet available" (PCT-9).</summary>
    NotYetAvailable = 0,

    SoldOut = 1,

    /// <summary>At or below the configured threshold: "only a few left".</summary>
    LowStock = 2,

    InStock = 3,
}

public static class Availability
{
    /// <summary>
    /// Classifies one variant from the stock the Warehouse reports. A variant that is not sellable, or that has no stock record, is
    /// <see cref="AvailabilityClass.NotYetAvailable"/> (never an error, PCT-9). Otherwise zero or less is sold out, up to and
    /// including <paramref name="lowStockThreshold"/> is low stock, and anything above is in stock. A threshold of 0 has no low band.
    /// </summary>
    public static AvailabilityClass Classify(bool sellable, int? available, int lowStockThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lowStockThreshold);
        if (!sellable || available is null)
        {
            return AvailabilityClass.NotYetAvailable;
        }

        return available.Value <= 0 ? AvailabilityClass.SoldOut
            : available.Value <= lowStockThreshold ? AvailabilityClass.LowStock
            : AvailabilityClass.InStock;
    }

    /// <summary>True for in stock and low stock. This is a display state, not a reservation: the cart's check is the authoritative one (PCT-11).</summary>
    public static bool IsPurchasable(this AvailabilityClass value) => value is AvailabilityClass.InStock or AvailabilityClass.LowStock;
}

/// <summary>The two flags a product row keeps for the listings: something to buy, and fit to appear (D-2).</summary>
public sealed record ProductVisibilityResult(bool IsInStock, bool IsListable);

public static class ProductVisibility
{
    /// <summary>
    /// A product is in stock when any variant can be bought, and listable when it is in stock and its main image is ready (the
    /// derivatives exist, PCT-7). Its page is visible whenever it is published, which is why the read model holds only published products.
    /// </summary>
    public static ProductVisibilityResult Evaluate(IEnumerable<AvailabilityClass> variants, bool primaryImageReady)
    {
        ArgumentNullException.ThrowIfNull(variants);
        var inStock = variants.Any(v => v.IsPurchasable());
        return new ProductVisibilityResult(inStock, inStock && primaryImageReady);
    }
}

/// <summary>One variant's price facts for <see cref="PriceSummary"/>.</summary>
public sealed record PricedVariant(decimal? Price, decimal? CompareAtPrice, AvailabilityClass Availability);

/// <summary>What a listing tile shows: the price range, whether the product is on sale and the biggest saving (PCT-4, Q-LST-2).</summary>
public sealed record PriceSummary(decimal MinPrice, decimal MaxPrice, bool OnSale, decimal MaxDiscountPercent)
{
    /// <summary>
    /// The summary over the variants that can be bought; when none can (sold out), over the priced ones that exist, so a sold-out page
    /// still shows a price. Variants that are not yet available never count. A product is on sale when a counted variant has a compare-at
    /// price above its price. Null when nothing is priced.
    /// </summary>
    public static PriceSummary? Of(IEnumerable<PricedVariant> variants)
    {
        ArgumentNullException.ThrowIfNull(variants);
        var priced = variants.Where(v => v.Price is > 0 && v.Availability != AvailabilityClass.NotYetAvailable).ToList();
        var counted = priced.Where(v => v.Availability.IsPurchasable()).ToList();
        if (counted.Count == 0)
        {
            counted = priced;
        }

        if (counted.Count == 0)
        {
            return null;
        }

        var savings = counted
            .Where(v => v.CompareAtPrice > v.Price)
            .Select(v => decimal.Round((v.CompareAtPrice!.Value - v.Price!.Value) / v.CompareAtPrice.Value * 100m, 2, MidpointRounding.AwayFromZero))
            .ToList();
        return new PriceSummary(
            counted.Min(v => v.Price!.Value),
            counted.Max(v => v.Price!.Value),
            savings.Count > 0,
            savings.Count > 0 ? savings.Max() : 0m);
    }
}
