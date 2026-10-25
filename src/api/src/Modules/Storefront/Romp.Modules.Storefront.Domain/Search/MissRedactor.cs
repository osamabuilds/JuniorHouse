using System.Text.RegularExpressions;

namespace Romp.Modules.Storefront.Domain.Search;

/// <summary>
/// Keeps personal data out of the zero-result log (SRC-13, SEC-25). A query that looks like an email address, a phone number or a link
/// is not stored at all, rather than stored with the sensitive part cut out.
/// </summary>
public static partial class MissRedactor
{
    /// <summary>The text to store for a missed search, or null when it must not be stored.</summary>
    public static string? ForStorage(SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.IsEmpty || query.Text.Contains('@', StringComparison.Ordinal) || UrlScheme().IsMatch(query.Text) || LongDigitRun().IsMatch(query.Text))
        {
            return null;
        }

        return query.Text;
    }

    // Seven digits, with the separators people put in phone numbers, make a number worth dropping.
    [GeneratedRegex(@"(?:\d[\s\-().+]*){7,}", RegexOptions.CultureInvariant)]
    private static partial Regex LongDigitRun();

    [GeneratedRegex(@"(?:^|\s)(?:[a-z][a-z0-9+.\-]*://|www\.)", RegexOptions.CultureInvariant)]
    private static partial Regex UrlScheme();
}
