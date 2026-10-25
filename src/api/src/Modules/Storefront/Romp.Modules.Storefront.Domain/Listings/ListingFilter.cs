using System.Globalization;
using System.Text.RegularExpressions;

namespace Romp.Modules.Storefront.Domain.Listings;

public enum ListingScopeKind
{
    /// <summary>Everything listable for one gender: <c>/girls</c>.</summary>
    Audience = 1,

    /// <summary><c>/girls/dresses</c>.</summary>
    Category = 2,

    /// <summary><c>/girls/new-in</c>.</summary>
    NewIn = 3,

    /// <summary><c>/girls/sale</c>.</summary>
    Sale = 4,

    /// <summary><c>/collections/summer</c>.</summary>
    Collection = 5,

    /// <summary><c>/new-in</c>, all audiences.</summary>
    AllNewIn = 6,

    /// <summary><c>/sale</c>, all audiences.</summary>
    AllSale = 7,

    /// <summary>Every listable product, newest first, with no window (the home page falls back to it when nothing is new).</summary>
    All = 8,
}

/// <summary>
/// Which products a listing is about, by URL segment (lower case, <c>-</c> for <c>_</c>); the application resolves the segments against the
/// lookups and answers 404 for one that does not exist (LST-11).
/// </summary>
public sealed record ListingScope(ListingScopeKind Kind, string? Gender = null, string? Category = null, string? Collection = null)
{
    public static ListingScope Audience(string gender) => new(ListingScopeKind.Audience, gender);

    public static ListingScope InCategory(string gender, string category) => new(ListingScopeKind.Category, gender, category);

    public static ListingScope NewIn(string? gender) => gender is null ? new(ListingScopeKind.AllNewIn) : new(ListingScopeKind.NewIn, gender);

    public static ListingScope Sale(string? gender) => gender is null ? new(ListingScopeKind.AllSale) : new(ListingScopeKind.Sale, gender);

    public static ListingScope Everything() => new(ListingScopeKind.All);

    public static ListingScope OfCollection(string collection) => new(ListingScopeKind.Collection, Collection: collection);
}

public enum ListingSort
{
    /// <summary>Newest published first (the default).</summary>
    Recent = 0,

    PriceAscending = 1,

    PriceDescending = 2,
}

/// <summary>A filter that was refused (a 400): which part and why.</summary>
public sealed record ListingFilterError(string Field, string Message);

/// <summary>
/// What a visitor narrowed a listing to, parsed and clamped from a query string (LST-1..4, 11, 16). Unknown keys are dropped by the caller;
/// values that are not a plausible code are ignored; numbers that are malformed are refused. The filter is a value: the same input
/// always gives the same canonical query, so a shared link has one spelling (SEO-06).
/// </summary>
public sealed record ListingFilter
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 48;
    public const int MaxValuesPerKey = 20;
    public const decimal MaxPrice = 1_000_000m;

    private static readonly Regex CodePattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex NumberPattern = new("^[0-9]+(\\.[0-9]{1,2})?$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public IReadOnlyList<string> Sizes { get; init; } = [];

    public IReadOnlyList<string> Colours { get; init; } = [];

    public string? Age { get; init; }

    public decimal? PriceMin { get; init; }

    public decimal? PriceMax { get; init; }

    public ListingSort Sort { get; init; } = ListingSort.Recent;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public bool HasVariantFilters => Sizes.Count > 0 || Colours.Count > 0 || PriceMin is not null || PriceMax is not null;

    public bool HasAny => HasVariantFilters || Age is not null;

    /// <summary>
    /// Parses the raw query values. <paramref name="price"/> is <c>min-max</c> with either side optional (<c>1000-3000</c>, <c>1000-</c>,
    /// <c>-3000</c>). Returns the filter, or the reasons it was refused.
    /// </summary>
    public static (ListingFilter? Filter, IReadOnlyList<ListingFilterError> Errors) Parse(
        IEnumerable<string?>? sizes, IEnumerable<string?>? colours, string? age, string? price, string? sort, string? page, string? pageSize)
    {
        var errors = new List<ListingFilterError>();

        decimal? min = null, max = null;
        if (!string.IsNullOrWhiteSpace(price))
        {
            var parts = price.Split('-');
            if (parts.Length != 2)
            {
                errors.Add(new ListingFilterError("price", "A price range is min-max, for example 1000-3000."));
            }
            else
            {
                min = ParsePrice(parts[0], "price", errors);
                max = ParsePrice(parts[1], "price", errors);
                if (min is not null && max is not null && min > max)
                {
                    errors.Add(new ListingFilterError("price", "The lowest price cannot be above the highest."));
                }
            }
        }

        var pageNumber = 1;
        if (!string.IsNullOrWhiteSpace(page) && (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out pageNumber) || pageNumber < 1))
        {
            // Not a whole number from 1: refused. A huge number that does parse is let through and answers 404 past the end.
            errors.Add(new ListingFilterError("page", "The page is a whole number from 1."));
            pageNumber = 1;
        }

        var size = DefaultPageSize;
        if (!string.IsNullOrWhiteSpace(pageSize))
        {
            if (!int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out size) || size < 1)
            {
                errors.Add(new ListingFilterError("pageSize", "The page size is a whole number from 1."));
                size = DefaultPageSize;
            }

            size = Math.Min(size, MaxPageSize);
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ListingFilter
        {
            Sizes = Codes(sizes),
            Colours = Codes(colours),
            Age = Codes([age]).FirstOrDefault(),
            PriceMin = min,
            PriceMax = max,
            Sort = ParseSort(sort),
            Page = pageNumber,
            PageSize = size,
        }, []);
    }

    /// <summary>The query string for this filter in one fixed key order, defaults left out, values sorted: one spelling per filter (SEO-06).</summary>
    public string ToCanonicalQuery(bool includePage = true)
    {
        var parts = new List<string>();
        if (Age is not null)
        {
            parts.Add("age=" + Age);
        }

        parts.AddRange(Sizes.Order(StringComparer.Ordinal).Select(s => "size=" + s));
        parts.AddRange(Colours.Order(StringComparer.Ordinal).Select(c => "colour=" + c));
        if (PriceMin is not null || PriceMax is not null)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"price={PriceMin:0.##}-{PriceMax:0.##}"));
        }

        if (Sort != ListingSort.Recent)
        {
            parts.Add("sort=" + (Sort == ListingSort.PriceAscending ? "price-asc" : "price-desc"));
        }

        if (includePage && Page > 1)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"page={Page}"));
        }

        return string.Join('&', parts);
    }

    private static decimal? ParsePrice(string text, string field, List<ListingFilterError> errors)
    {
        if (text.Length == 0)
        {
            return null;
        }

        if (!NumberPattern.IsMatch(text) || !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value > MaxPrice)
        {
            errors.Add(new ListingFilterError(field, $"A price is a number from 0 to {MaxPrice:0}."));
            return null;
        }

        return value;
    }

    private static ListingSort ParseSort(string? sort) => sort switch
    {
        "price-asc" => ListingSort.PriceAscending,
        "price-desc" => ListingSort.PriceDescending,
        _ => ListingSort.Recent,
    };

    /// <summary>Lower-cases, drops anything that is not a plausible code, removes duplicates and keeps at most <see cref="MaxValuesPerKey"/>.</summary>
    private static List<string> Codes(IEnumerable<string?>? values) =>
        (values ?? [])
            .Select(v => v?.Trim().ToLowerInvariant())
            .Where(v => v is { Length: > 0 and <= 40 } && CodePattern.IsMatch(v))
            .Select(v => v!)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxValuesPerKey)
            .ToList();
}

/// <summary>The same-variant rule (Q-LST-9): size, colour and price must all hold on one in-stock variant.</summary>
public static class ListingRules
{
    public static bool Matches(bool inStock, string sizeSegment, string colourSegment, decimal? price, ListingFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (!inStock)
        {
            return false;
        }

        if (filter.Sizes.Count > 0 && !filter.Sizes.Contains(sizeSegment))
        {
            return false;
        }

        if (filter.Colours.Count > 0 && !filter.Colours.Contains(colourSegment))
        {
            return false;
        }

        if (filter.PriceMin is not null && (price is null || price < filter.PriceMin))
        {
            return false;
        }

        return filter.PriceMax is null || (price is not null && price <= filter.PriceMax);
    }

    /// <summary>True when the page asked for does not exist: a page after the last one (page 1 always exists, even if empty).</summary>
    public static bool IsPastTheEnd(int page, int pageSize, int total) =>
        page > 1 && (long)(page - 1) * pageSize >= total;
}
