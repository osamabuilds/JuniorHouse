namespace Romp.Modules.Catalog.Contracts;

/// <summary>A lookup value as the storefront shows and filters by it: the stable code (for URLs and filters) and the display name.</summary>
public sealed record PublishedCode(short Id, string Code, string Name);

/// <summary>
/// One size × colour of a published product. <c>IsSellable</c> is true when Labelling issued a live GTIN and the variant is priced; the
/// GTIN itself is not part of the contract, and the SKU is set only for a sellable variant (an unlabelled, voided or unpriced one is not
/// findable by SKU).
/// </summary>
public sealed record PublishedVariant(
    long VariantId,
    PublishedCode Colour,
    PublishedCode Size,
    string? SkuCode,
    decimal? Price,
    decimal? CompareAtPrice,
    bool IsSellable);

/// <summary>An image of a product (<c>ColourId</c> null) or of one of its colours. <c>StorageKey</c> addresses the original in shared file storage.</summary>
public sealed record PublishedImage(long ImageId, short? ColourId, string StorageKey, int Width, int Height, string AltText, int SortSeq);

public sealed record PublishedCompositionLine(PublishedCode Fibre, decimal Percent);

public sealed record PublishedCareInstruction(PublishedCode Care, string SymbolCode);

/// <summary>
/// Everything the storefront shows about one published product, as one full snapshot (published-catalog plan, PCT-1). <c>Version</c> is
/// the product's content version: the storefront keeps the highest it has seen, so repeated or reordered snapshots cannot regress it
/// (PCT-5). No staff names, audit data or GTINs.
/// </summary>
public sealed record PublishedProductSnapshot(
    long ProductId,
    long Version,
    string Path,
    string Name,
    string Description,
    string StyleCode,
    PublishedCode Gender,
    PublishedCode Category,
    PublishedCode AgeBracket,
    IReadOnlyList<PublishedCode> Collections,
    DateTimeOffset PublishedAt,
    string? CareNote,
    IReadOnlyList<PublishedVariant> Variants,
    IReadOnlyList<PublishedImage> Images,
    IReadOnlyList<PublishedCompositionLine> Composition,
    IReadOnlyList<PublishedCareInstruction> Care);

/// <summary>A published product and its content version, for cheap reconciliation (PCT-5, PCT-6).</summary>
public sealed record PublishedProductVersion(long ProductId, long Version);

/// <summary>
/// The Catalog contract the storefront read model is built from: the only source of published product data (ADR 0018). Only Published
/// products appear; a draft, unpublished or archived product reads as null / absent. Paged by product id, so a rebuild walks the whole
/// catalogue in a constant number of queries per page.
/// </summary>
public interface IPublishedProductQueries
{
    /// <summary>The snapshot of one product, or null when it is not published.</summary>
    Task<PublishedProductSnapshot?> GetSnapshotAsync(long productId, CancellationToken cancellationToken);

    /// <summary>Published products with a higher id than <paramref name="afterProductId"/>, ordered by id: id and version only.</summary>
    Task<IReadOnlyList<PublishedProductVersion>> ListVersionsAsync(long afterProductId, int take, CancellationToken cancellationToken);

    /// <summary>Snapshots of published products with a higher id than <paramref name="afterProductId"/>, ordered by id.</summary>
    Task<IReadOnlyList<PublishedProductSnapshot>> ListSnapshotsAsync(long afterProductId, int take, CancellationToken cancellationToken);
}
