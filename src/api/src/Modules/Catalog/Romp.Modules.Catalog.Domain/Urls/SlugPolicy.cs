using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Romp.Modules.Catalog.Domain.Urls;

/// <summary>
/// The pure rules for a product slug (URL-4, URL-6..8, ADR 0013): a closed alphabet (<c>a-z</c>, <c>0-9</c>, single hyphens), at most
/// <see cref="DefaultMaxLength"/> characters, never a reserved word. Everything that is not in the alphabet is dropped or turned into a
/// separator, so path-traversal text, percent-encoding, mixed case and Unicode look-alikes cannot survive.
/// </summary>
public static class SlugPolicy
{
    public const int DefaultMaxLength = 80;

    /// <summary>Words that add nothing to a URL; dropped when other words remain.</summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal) { "a", "an", "the", "and", "of", "for", "with" };

    private static readonly Regex Pattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Turns free text into a slug candidate; returns an empty string when nothing usable remains.</summary>
    public static string Normalise(string? text, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormKD).ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                current.Append(c);
            }
            else if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                // The accent of a decomposed letter: dropped, the letter stays (e-acute becomes e).
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        var meaningful = words.Where(w => !StopWords.Contains(w)).ToList();
        var chosen = meaningful.Count > 0 ? meaningful : words;
        return Cut(string.Join('-', chosen), maxLength);
    }

    /// <summary>The first problem with a slug, or null when it is acceptable (URL-6, URL-7, URL-8).</summary>
    public static string? Validate(string? slug, IReadOnlyCollection<string> reserved, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return "The URL name is required.";
        }

        if (slug.Length > maxLength)
        {
            return $"The URL name can be at most {maxLength} characters.";
        }

        if (!Pattern.IsMatch(slug))
        {
            return "The URL name may contain only lower-case letters, digits and single hyphens between words.";
        }

        return reserved.Contains(slug, StringComparer.Ordinal) ? $"\"{slug}\" is reserved and cannot be used as a URL name." : null;
    }

    /// <summary>The slug with a numeric suffix (<c>floral-dress-2</c>), still within the maximum length (URL-4).</summary>
    public static string WithSuffix(string slug, int number, int maxLength = DefaultMaxLength)
    {
        var suffix = "-" + number.ToString(CultureInfo.InvariantCulture);
        var room = Math.Max(1, maxLength - suffix.Length);
        return Cut(slug, room) + suffix;
    }

    private static string Cut(string slug, int maxLength)
    {
        if (slug.Length <= maxLength)
        {
            return slug.Trim('-');
        }

        var cut = slug[..maxLength];
        if (slug[maxLength] != '-')
        {
            var lastHyphen = cut.LastIndexOf('-');
            if (lastHyphen > 0)
            {
                cut = cut[..lastHyphen];
            }
        }

        return cut.Trim('-');
    }
}
