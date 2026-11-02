namespace Romp.Modules.Catalog.Contracts;

/// <summary>
/// What a basket or an order needs to know about one buyable variant, read from the published catalogue (cart plan, C2). The
/// <see cref="VariantId"/> is the planned-variant id the storefront already shows on the product page; <see cref="LabellingVariantId"/> is the
/// id Warehouse keys stock by, so the cart can ask for availability without reading anyone else's tables (ADR 0002, ADR 0014).
/// </summary>
public sealed record VariantSale(
    long VariantId,
    long ProductId,
    string ProductName,
    string Path,
    string ColourName,
    string SizeName,
    string? SkuCode,
    long? LabellingVariantId,
    decimal? Price,
    decimal? CompareAtPrice,
    bool IsSellable,
    short CategoryId,
    long? ImageId,
    string? ImageKey,
    string? ImageAlt,
    int? ImageWidth,
    int? ImageHeight);

/// <summary>
/// Variant facts for the cart and checkout in one bulk call (never one call per line, so no N+1). Only variants of a <b>published</b> product
/// are returned; an id that is unknown, unpublished or archived is simply absent, and the caller treats absent as "not for sale" (CRT-5).
/// </summary>
public interface IVariantSalesQueries
{
    /// <summary>The most variants one call accepts.</summary>
    public const int MaxIds = 100;

    Task<IReadOnlyList<VariantSale>> GetManyAsync(IReadOnlyCollection<long> variantIds, CancellationToken cancellationToken);
}
