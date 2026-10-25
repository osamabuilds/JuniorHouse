namespace Romp.Modules.Storefront.Domain.Search;

/// <summary>
/// Builds the text of a PostgreSQL <c>tsquery</c> from expanded tokens (search plan, "Matching"). Tokens are letters and digits only (the
/// tokeniser guarantees it, and this re-checks), so the text holds no operator the visitor chose; it is still passed as a parameter, never
/// joined into SQL. Words are prefix-matched, alternatives are OR-ed, positions are AND-ed.
/// </summary>
public static class TsQueryText
{
    /// <summary>The query text, or null when there is nothing to match.</summary>
    public static string? From(IReadOnlyList<IReadOnlyList<IReadOnlyList<string>>> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var parts = new List<string>();
        foreach (var alternatives in positions)
        {
            var options = alternatives.Select(Phrase).Where(p => p is not null).Select(p => p!).Distinct(StringComparer.Ordinal).ToList();
            if (options.Count > 0)
            {
                parts.Add(options.Count == 1 ? options[0] : $"({string.Join(" | ", options)})");
            }
        }

        return parts.Count == 0 ? null : string.Join(" & ", parts);
    }

    private static string? Phrase(IReadOnlyList<string> words)
    {
        var clean = words.Where(w => w.Length > 0 && w.All(char.IsLetterOrDigit)).Select(w => w + ":*").ToList();
        return clean.Count switch
        {
            0 => null,
            1 => clean[0],
            _ => $"({string.Join(" <-> ", clean)})",
        };
    }
}
