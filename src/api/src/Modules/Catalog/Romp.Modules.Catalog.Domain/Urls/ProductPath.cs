namespace Romp.Modules.Catalog.Domain.Urls;

/// <summary>
/// A product's public path: <c>/{gender}/{category}/{slug}</c> (ADR 0013, owner decision D2: age is a filter, never a segment).
/// Gender and category segments are their lookup codes in lower case; the slug is the product's own. No id and no query string
/// ever appears in it (URL-10).
/// </summary>
public sealed record ProductPath(string GenderSegment, string CategorySegment, string Slug)
{
    public override string ToString() => $"/{GenderSegment}/{CategorySegment}/{Slug}";

    /// <summary>The path segment of a lookup code: lower case, underscores as hyphens.</summary>
    public static string SegmentOf(string lookupCode) => lookupCode.Trim().ToLowerInvariant().Replace('_', '-');

    public static ProductPath Build(string genderCode, string categoryCode, string slug) =>
        new(SegmentOf(genderCode), SegmentOf(categoryCode), slug);

    /// <summary>Splits a canonical path into its parts; null for anything else (a query string, a fragment, another depth).</summary>
    public static ProductPath? Parse(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('?', StringComparison.Ordinal) || path.Contains('#', StringComparison.Ordinal))
        {
            return null;
        }

        var parts = path.Trim().Trim('/').Split('/');
        return parts.Length == 3 && parts.All(p => p.Length > 0) ? new ProductPath(parts[0], parts[1], parts[2]) : null;
    }

    /// <summary>
    /// The canonical spelling of a requested path (URL-5): lower case, one leading slash, no trailing slash. The storefront answers
    /// a request whose path differs from this with a 301 to this value.
    /// </summary>
    public static string Canonicalise(string? path)
    {
        var trimmed = (path ?? string.Empty).Trim().Trim('/').ToLowerInvariant();
        return "/" + trimmed;
    }
}
