namespace Romp.Modules.Warehouse.Contracts;

/// <summary>
/// What can be sold of one variant right now. <c>VariantId</c> is Labelling's variant id, the one the stock events carry. <c>Version</c>
/// is the variant's stock version: it goes up by one with every stock posting that touches the variant, inside that posting's
/// transaction, so the storefront keeps the highest version it has seen and a repeated or reordered message cannot regress it (PCT-5).
/// </summary>
public sealed record StockAvailability(long VariantId, string SkuCode, int Available, long Version);

/// <summary>
/// The Warehouse contract the storefront read model is built from (ADR 0018). Only variants that have ever had a stock posting appear:
/// a variant with no row is "not yet available" (PCT-9). Paged by variant id, so a rebuild walks every variant in a constant number of
/// queries per page. Available equals on-hand until reservations exist.
/// </summary>
public interface IStockAvailabilityQueries
{
    /// <summary>Availability of variants with a higher id than <paramref name="afterVariantId"/>, ordered by variant id (at most 500 per call).</summary>
    Task<IReadOnlyList<StockAvailability>> ListAvailabilityAsync(long afterVariantId, int take, CancellationToken cancellationToken);

    /// <summary>The availability of one variant, or null when it has never had a stock posting.</summary>
    Task<StockAvailability?> GetAsync(long variantId, CancellationToken cancellationToken);
}
