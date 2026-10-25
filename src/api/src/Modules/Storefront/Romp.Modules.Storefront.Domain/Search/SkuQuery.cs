using System.Text.RegularExpressions;

namespace Romp.Modules.Storefront.Domain.Search;

/// <summary>
/// Decides whether typed text could be a SKU (<c>ROMP-STYLE-COLOUR-SIZE</c>) or a style code, so the page can try an exact lookup first
/// (FR-06, SRC-4, 5). Only a shape check: the lookup itself decides whether anything matches.
/// </summary>
public static partial class SkuQuery
{
    /// <summary>The SKU in the form stored in the read model (<c>SKU_NORM</c>): trimmed and lower case. Null when the text cannot be a code.</summary>
    public static string? TryParse(string? raw)
    {
        var text = SearchQuery.Normalise(raw);
        return text.Length is >= 2 and <= 120 && CodeShape().IsMatch(text) ? text : null;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeShape();
}
