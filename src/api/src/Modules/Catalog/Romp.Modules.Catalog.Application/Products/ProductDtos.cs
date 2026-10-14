namespace Romp.Modules.Catalog.Application.Products;

/// <summary>One planned size × colour with its price and Labelling's facts. <c>Availability</c> is <c>Sellable</c>, <c>Unpriced</c>, <c>GtinVoided</c> or <c>NotYetAvailable</c>.</summary>
public sealed record VariantDto(
    long Id,
    short ColourId,
    string ColourName,
    short SizeId,
    string SizeName,
    decimal? Price,
    decimal? CompareAtPrice,
    string? SkuCode,
    string? Gtin,
    string Availability,
    bool IsSellable);

/// <summary>One thing stopping a publish, with the editor tab that fixes it (PRD-4).</summary>
public sealed record GateFailureDto(string Code, string Message, string Tab);

/// <summary>What publishing would do right now: every failure at once, and advice that does not block (such as "no size chart").</summary>
public sealed record PublishGatePreviewDto(IReadOnlyList<GateFailureDto> Failures, IReadOnlyList<string> Warnings);

/// <summary>
/// The full product for the editor (PRD-1). <c>Version</c> is the concurrency token: send it back with every edit (PRD-5).
/// <c>Status</c> is the lookup code (<c>Draft</c>, <c>Published</c>, <c>Unpublished</c>, <c>Archived</c>).
/// </summary>
public sealed record ProductDto(
    long Id,
    long StyleId,
    string StyleCode,
    string Name,
    string Description,
    short GenderId,
    short CategoryId,
    short AgeBracketId,
    short StatusId,
    string Status,
    string Slug,
    string Path,
    string? CareNote,
    DateTimeOffset? PublishedAt,
    string? PublishedBy,
    uint Version,
    IReadOnlyList<short> CollectionIds,
    IReadOnlyList<VariantDto> Variants,
    int SellableVariants,
    PublishGatePreviewDto Gate);

/// <summary>A row of the product list. <c>SellableVariants</c> counts planned variants that are priced and have a live GTIN.</summary>
public sealed record ProductRowDto(
    long Id,
    long StyleId,
    string StyleCode,
    string Name,
    short GenderId,
    short CategoryId,
    short StatusId,
    string Status,
    string Slug,
    int PlannedVariants,
    int SellableVariants,
    DateTimeOffset? PublishedAt);

/// <summary>The result of a publish attempt: the product when it went live, or every failure when it did not (PRD-4).</summary>
public sealed record PublishProductResult(bool Published, ProductDto Product, IReadOnlyList<GateFailureDto> Failures);

/// <summary>A price for one variant, as typed by staff (PRD-2).</summary>
public sealed record VariantPriceInput(short ColourId, short SizeId, decimal Price, decimal? CompareAtPrice);
