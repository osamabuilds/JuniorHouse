namespace Romp.Modules.Storefront.Contracts;

/// <summary>
/// A published product as another module may show it: where it lives, what it costs now and whether it can be bought. <see cref="Path"/> is the
/// site-relative page address (no host). <see cref="WasPrice"/> is the struck-through price while the product is on sale.
/// <see cref="MainImageId"/> goes to <see cref="IStorefrontImageQueries"/> for a picture.
/// </summary>
public sealed record ProductSummary(
    long ProductId, string Name, string Path, decimal Price, decimal? WasPrice, bool IsAvailable, long? MainImageId);

/// <summary>
/// One buyable variant (size and colour) of a published product as another module may show it. <see cref="Path"/> is the site-relative page
/// address. <see cref="IsAvailable"/> is false while no units can be bought (sold out, or no stock known yet).
/// </summary>
public sealed record VariantSummary(
    long VariantId, long ProductId, string ProductName, string Path, string ColourName, string SizeName, string? SkuCode, bool IsAvailable);

/// <summary>
/// What other modules may ask Storefront about its published products (ADR 0002: through Contracts only). One bulk call for any number of
/// products, never one call per product. A product that is not published (or has no price yet) is simply absent, a sold-out one is returned with IsAvailable false, and the caller treats it as
/// no longer available.
/// </summary>
public interface IStorefrontCatalogQueries
{
    /// <summary>The most products one call accepts.</summary>
    public const int MaxIds = 200;

    Task<IReadOnlyList<ProductSummary>> GetProductSummariesAsync(IReadOnlyCollection<long> productIds, CancellationToken cancellationToken);

    /// <summary>
    /// Variants by id, in one call for any number up to <see cref="MaxIds"/>. A variant that does not exist, is not sellable, or whose product
    /// is not published is absent; a sold-out one is returned with IsAvailable false.
    /// </summary>
    Task<IReadOnlyList<VariantSummary>> GetVariantSummariesAsync(IReadOnlyCollection<long> variantIds, CancellationToken cancellationToken);
}
