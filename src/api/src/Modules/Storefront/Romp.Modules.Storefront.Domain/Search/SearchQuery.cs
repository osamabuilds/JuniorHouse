using System.Globalization;
using System.Text;

namespace Romp.Modules.Storefront.Domain.Search;

/// <summary>
/// What a visitor typed, made safe to match on (search spec SRC-3, 8). Everything that reaches the database is a token that passed the
/// letters-and-digits allow-list, so no operator or quote the visitor types can mean anything to a query.
/// </summary>
public sealed record SearchQuery
{
    public const int MaxLength = 100;
    public const int MaxTokens = 8;
    public const int MaxTokenLength = 40;

    private SearchQuery(string text, IReadOnlyList<string> tokens)
    {
        Text = text;
        Tokens = tokens;
    }

    /// <summary>The normalised text: lower case, one space between words, at most <see cref="MaxLength"/> characters. Safe to echo once encoded.</summary>
    public string Text { get; }

    /// <summary>Words of 1 to 40 letters or digits, at most eight; the only text that is matched.</summary>
    public IReadOnlyList<string> Tokens { get; }

    public bool IsEmpty => Tokens.Count == 0;

    public static SearchQuery Parse(string? raw)
    {
        var text = Normalise(raw);
        return new SearchQuery(text, Tokenise(text));
    }

    /// <summary>NFKC, lower case, control and format (bidi, zero-width) characters removed, whitespace collapsed, trimmed, cut at 100 characters.</summary>
    public static string Normalise(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string folded;
        try
        {
            // Bound the work first: a megabyte of input must not cost a megabyte of normalisation.
            var bounded = raw.Length > MaxLength * 4 ? raw[..(MaxLength * 4)] : raw;
            folded = bounded.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            // Lone surrogates and similar cannot be normalised; treat the text as nothing rather than failing the request.
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(folded.Length, MaxLength));
        var pendingSpace = false;
        foreach (var rune in folded.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned)
            {
                if (Rune.IsWhiteSpace(rune))
                {
                    pendingSpace = builder.Length > 0;
                }

                continue;
            }

            if (Rune.IsWhiteSpace(rune) || category is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(Rune.ToLowerInvariant(rune).ToString());
        }

        var text = builder.ToString();
        if (text.Length > MaxLength)
        {
            text = text[..MaxLength];
            // Never leave half a surrogate pair at the cut.
            if (char.IsHighSurrogate(text[^1]))
            {
                text = text[..^1];
            }

            text = text.TrimEnd();
        }

        return text;
    }

    private static List<string> Tokenise(string normalised)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (var c in normalised)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
                continue;
            }

            Flush(tokens, current);
            if (tokens.Count == MaxTokens)
            {
                return tokens;
            }
        }

        Flush(tokens, current);
        return tokens;
    }

    private static void Flush(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        if (tokens.Count < MaxTokens)
        {
            var token = current.Length > MaxTokenLength ? current.ToString(0, MaxTokenLength) : current.ToString();
            tokens.Add(token);
        }

        current.Clear();
    }
}
