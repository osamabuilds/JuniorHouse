using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.Listings;

namespace Romp.Modules.Storefront.Application.ProductPages;

/// <summary>One resized picture of a gallery image: the WebP file and its JPEG fallback at one width. Both are <c>/media/{name}</c> URLs.</summary>
public sealed record ProductImageSourceDto(int Width, string Webp, string? Jpeg);

public sealed record ProductImageDto(string AltText, int Width, int Height, IReadOnlyList<ProductImageSourceDto> Sources);

/// <summary>A colour the product comes in. <c>InStock</c> is false when none of its sizes can be bought (shown as a text note, not by colour alone).</summary>
public sealed record ColourOptionDto(string Code, string Name, bool InStock);

/// <summary>
/// A size of the selected colour. <c>Availability</c> is one of InStock, LowStock, SoldOut or NotYetAvailable (PDP-4, 5); there is never a
/// count (PCT-3). <c>Selectable</c> is false for the last two.
/// </summary>
public sealed record SizeOptionDto(string Code, string Name, string Availability, bool Selectable);

/// <summary>The price line. <c>IsFrom</c> marks "From PKR x" before a size is chosen (PDP-15); <c>CompareAtPrice</c> is set only when above the price (PDP-9); <c>SavingAmount</c> is the server difference in PKR (SEC-09), zero when there is no discount.</summary>
public sealed record ProductPriceDto(decimal Price, decimal? CompareAtPrice, int SavingPercent, bool IsFrom, decimal SavingAmount);

public sealed record CompositionLineDto(string Name, decimal Percent);

/// <summary>A care instruction: its words always, and the symbol code that names the pictogram once the artwork is licensed (Q-PDP-3).</summary>
public sealed record CareLineDto(string Name, string SymbolCode);

public sealed record SizeChartRowDto(
    string SizeCode,
    string SizeName,
    int? AgeMinMonths,
    int? AgeMaxMonths,
    decimal? HeightMinCm,
    decimal? HeightMaxCm,
    decimal? WeightMinKg,
    decimal? WeightMaxKg,
    decimal? ChestCm,
    decimal? WaistCm,
    decimal? LengthCm,
    bool Selected);

public sealed record SizeChartDto(string Name, IReadOnlyList<SizeChartRowDto> Rows);

/// <summary>What the product page shows (PDP-1..11, 15..17). Every amount is the server's; nothing here is a stock count.</summary>
public sealed record ProductPageDto(
    string Path,
    string Name,
    string Description,
    string AudienceName,
    string CategoryName,
    string? AgeName,
    IReadOnlyList<BreadcrumbDto> Breadcrumbs,
    ProductPriceDto? Price,
    string SelectedColour,
    string? SelectedSize,
    string? Sku,
    IReadOnlyList<ColourOptionDto> Colours,
    IReadOnlyList<SizeOptionDto> Sizes,
    IReadOnlyList<ProductImageDto> Images,
    SizeChartDto? SizeChart,
    IReadOnlyList<CompositionLineDto> Composition,
    IReadOnlyList<CareLineDto> Care,
    string? CareNote,
    bool IsAvailable);

public enum ProductPageOutcome
{
    /// <summary>The page is served (200). A product with nothing in stock is still found (PDP-16).</summary>
    Found = 1,

    /// <summary>An old or non-normalised address: answer 301 to <c>Path</c> (PDP-13).</summary>
    Moved = 2,

    /// <summary>Archived for good: 410.</summary>
    Gone = 3,

    /// <summary>No such page, an unpublished product, or a product not projected yet: 404.</summary>
    NotFound = 4,
}

/// <summary>
/// The one shape the SSR layer reads and turns into a status code (plan: the API answers 200 for all four so the contract stays simple).
/// <c>Path</c> is set for Moved and comes from Catalog, never from the request; <c>Page</c> only for Found.
/// </summary>
public sealed record ProductPageResultDto(string Outcome, string? Path, ProductPageDto? Page) : IPublicStorefrontResponse;
