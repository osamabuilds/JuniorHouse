using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain;
using Romp.Modules.Storefront.Domain.Listings;

namespace Romp.Modules.Storefront.Application.Listings;

/// <summary>Listing configuration, behind a port so Application never reads configuration itself.</summary>
public interface IListingSettings
{
    /// <summary>New In shows products published within this many days (LST-7, Q-LST-3 default 30).</summary>
    int NewArrivalsDays { get; }
}

/// <summary>One resized picture of the main image: the WebP file and its JPEG fallback at one width. Both are <c>/media/{name}</c> URLs.</summary>
public sealed record ListingImageSourceDto(int Width, string Webp, string? Jpeg);

public sealed record ListingImageDto(string AltText, int Width, int Height, IReadOnlyList<ListingImageSourceDto> Sources);

/// <summary>
/// One product tile (LST-12). The price is the lowest among the variants that match the filter; the compare-at price is the one of that
/// variant; <c>SavingAmount</c> is the server difference in PKR (SEC-09). There is no stock count: only whether some sizes are sold out (PCT-3)
/// and, per size, whether it is sold out.
/// </summary>
public sealed record ListingTileDto(
    string Path,
    string Name,
    decimal? Price,
    decimal? CompareAtPrice,
    int SavingPercent,
    decimal SavingAmount,
    int ColourCount,
    bool HasSoldOutSizes,
    IReadOnlyList<ListingTileSizeDto> Sizes,
    ListingImageDto? Image);

/// <summary>A size the product comes in. <c>SoldOut</c> is true when no colour of that size can be bought; it is never hidden, so a shopper sees the whole range.</summary>
public sealed record ListingTileSizeDto(string Code, string Name, bool SoldOut);

public sealed record BreadcrumbDto(string Label, string? Path);

/// <summary>A page of a listing, with what the page needs to describe itself.</summary>
public sealed record ListingPageDto(
    string Title,
    string ScopeKind,
    string? AudienceName,
    string? CategoryName,
    IReadOnlyList<BreadcrumbDto> Breadcrumbs,
    IReadOnlyList<ListingTileDto> Tiles,
    int Total,
    int Page,
    int PageSize,
    int TotalPages,
    int MaxSavingPercent) : IPublicStorefrontResponse;

public sealed record ListingOptionDto(string Value, string Label);

/// <summary>The filter choices that exist in a scope: sizes and colours with a product in stock, and the price range (LST-1, 2).</summary>
public sealed record ListingOptionsDto(
    IReadOnlyList<ListingOptionDto> Sizes,
    IReadOnlyList<ListingOptionDto> Colours,
    decimal? PriceMin,
    decimal? PriceMax,
    IReadOnlyList<ListingOptionDto> Ages) : IPublicStorefrontResponse;

/// <summary>A link offered when a listing is empty (LST-5, 6): another category, an audience or New In.</summary>
public sealed record SuggestionDto(string Label, string Path);

public sealed record EmptyStateDto(IReadOnlyList<SuggestionDto> Suggestions) : IPublicStorefrontResponse;

public enum ListingOutcome
{
    Found = 1,

    /// <summary>A gender, category or collection in the URL that is not a lookup row (404).</summary>
    ScopeNotFound = 2,

    /// <summary>A page after the last one (404, LST-14).</summary>
    PastTheEnd = 3,
}

public sealed record ListingResult<T>(ListingOutcome Outcome, T? Value)
    where T : class
{
}

/// <summary>Builds listing results (kept off the generic record: a static member on a generic type is not allowed).</summary>
public static class ListingResult
{
    public static ListingResult<T> Of<T>(T value) where T : class => new(ListingOutcome.Found, value);

    public static ListingResult<T> Failed<T>(ListingOutcome outcome) where T : class => new(outcome, null);
}

/// <summary>A scope whose segments were matched to lookup rows.</summary>
public sealed record ResolvedScope(ListingScope Scope, LookupRow? Gender, LookupRow? Category, LookupRow? Collection)
{
    public string Title => Scope.Kind switch
    {
        ListingScopeKind.Audience => Gender!.Name,
        ListingScopeKind.Category => Category!.Name,
        ListingScopeKind.NewIn or ListingScopeKind.AllNewIn => "New in",
        ListingScopeKind.Sale or ListingScopeKind.AllSale => "Sale",
        ListingScopeKind.Collection => Collection!.Name,
        ListingScopeKind.All => "All products",
        _ => string.Empty,
    };

    /// <summary>Matches the URL segments of <paramref name="scope"/> to active lookup rows; null when one does not exist.</summary>
    public static ResolvedScope? Resolve(ListingScope scope, CatalogLookups lookups)
    {
        LookupRow? gender = null, category = null, collection = null;
        if (scope.Gender is not null && (gender = Find(lookups.Genders, scope.Gender)) is null)
        {
            return null;
        }

        if (scope.Category is not null && (category = Find(lookups.Categories, scope.Category)) is null)
        {
            return null;
        }

        if (scope.Collection is not null && (collection = Find(lookups.Collections, scope.Collection)) is null)
        {
            return null;
        }

        return new ResolvedScope(scope, gender, category, collection);
    }

    private static LookupRow? Find(IEnumerable<LookupRow> rows, string segment) =>
        rows.FirstOrDefault(r => r.IsActive && CodeSegment.Of(r.Code) == segment);
}
