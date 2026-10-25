namespace Romp.Modules.Storefront.Domain.Search;

/// <summary>
/// The words a product can be found by besides its name and description: style code, audience, category, age bracket, collections and
/// colours (FEAT-CAT-05, SRC-8). Stored on the product row (<c>SRCH_TXT</c>) so full-text and trigram matching read one column.
/// </summary>
public static class SearchText
{
    public const int MaxLength = 1000;

    public static string Build(
        string name, string styleCode, string genderName, string categoryName, string ageName,
        IEnumerable<string> collectionNames, IEnumerable<string> colourNames)
    {
        var words = new[] { name, styleCode, genderName, categoryName, ageName }
            .Concat(collectionNames)
            .Concat(colourNames)
            .Select(SearchQuery.Normalise)
            .Where(w => w.Length > 0)
            .Distinct(StringComparer.Ordinal);

        var text = string.Join(' ', words);
        return text.Length <= MaxLength ? text : text[..MaxLength].TrimEnd();
    }
}
