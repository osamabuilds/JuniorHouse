namespace Romp.Modules.Storefront.Domain.Search;

/// <summary>
/// Synonym groups held as data (SRC-12). A group is a set of terms that mean the same thing to a shopper (<c>frock</c>, <c>dress</c>,
/// <c>gown</c>); a search for any term also matches the others. Single words expand per token; multi-word terms are matched on the whole
/// normalised query first (<c>shalwar kameez</c>).
/// </summary>
public sealed class SynonymExpansion
{
    public static readonly SynonymExpansion None = new([]);

    private readonly Dictionary<string, string[]> groupOfTerm;

    public SynonymExpansion(IEnumerable<IReadOnlyCollection<string>> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        groupOfTerm = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var terms = group.Select(t => SearchQuery.Normalise(t)).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
            foreach (var term in terms)
            {
                // A term belongs to one group (the table enforces it); the first one wins if data ever disagrees.
                groupOfTerm.TryAdd(term, terms);
            }
        }
    }

    /// <summary>
    /// The alternatives for each position of the query: a list of alternative term lists. Each inner list holds tokens (single words) that
    /// are OR-ed; the outer list is AND-ed. A multi-word synonym contributes its words as one alternative only when every word of it is a token.
    /// With <paramref name="lastWordIsPartial"/> (autosuggest) the last word also matches synonym terms that start with it.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<string>>> Expand(SearchQuery query, bool lastWordIsPartial = false)
    {
        ArgumentNullException.ThrowIfNull(query);
        var result = new List<IReadOnlyList<IReadOnlyList<string>>>();
        var tokens = query.Tokens;
        var i = 0;
        while (i < tokens.Count)
        {
            var matched = TryPhrase(tokens, i, out var length, out var group);
            if (!matched)
            {
                length = 1;
                group = groupOfTerm.GetValueOrDefault(tokens[i]);
                if (group is null && lastWordIsPartial && i == tokens.Count - 1)
                {
                    // Still typing: "kurt" should find what "kurta" finds.
                    group = PartialGroup(tokens[i]);
                }
            }

            var alternatives = new List<IReadOnlyList<string>> { tokens.Skip(i).Take(length).ToArray() };
            if (group is not null)
            {
                foreach (var term in group)
                {
                    var words = TokensOf(term);
                    if (words.Length > 0 && !alternatives.Any(a => a.SequenceEqual(words)))
                    {
                        alternatives.Add(words);
                    }
                }
            }

            result.Add(alternatives);
            i += length;
        }

        return result;
    }

    private string[]? PartialGroup(string prefix)
    {
        var terms = groupOfTerm
            .Where(entry => !entry.Key.Contains(' ', StringComparison.Ordinal) && entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .SelectMany(entry => entry.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return terms.Length == 0 ? null : terms;
    }

    private bool TryPhrase(IReadOnlyList<string> tokens, int start, out int length, out string[]? group)
    {
        // Longest phrase first (up to three words).
        for (var n = Math.Min(3, tokens.Count - start); n >= 2; n--)
        {
            var phrase = string.Join(' ', tokens.Skip(start).Take(n));
            if (groupOfTerm.TryGetValue(phrase, out var found))
            {
                length = n;
                group = found;
                return true;
            }
        }

        length = 0;
        group = null;
        return false;
    }

    private static string[] TokensOf(string term) => SearchQuery.Parse(term).Tokens.ToArray();
}
