namespace Romp.Modules.Storefront.Domain.Navigation;

/// <summary>A lookup row as the menu needs it (Reference's code, display name and order).</summary>
public sealed record MenuLookup(string Code, string Name, short SortSeq, bool IsActive);

public enum MenuEntryKind
{
    NewIn = 1,
    Sale = 2,
    Category = 3,
    Collection = 4,
    Age = 5,
}

/// <summary>One link in the menu. <see cref="Code"/> is the stable lookup code (empty for New In and Sale); <see cref="Path"/> is a clean URL.</summary>
public sealed record MenuEntry(MenuEntryKind Kind, string Code, string Label, string Path);

/// <summary>An audience (a gender lookup row) and everything offered under it.</summary>
public sealed record MenuAudience(string Code, string Name, string Path, IReadOnlyList<MenuEntry> Entries);

/// <summary>The whole header menu. The shared entries are links to listings across all audiences.</summary>
public sealed record MenuTree(
    IReadOnlyList<MenuAudience> Audiences,
    IReadOnlyList<MenuEntry> Collections,
    bool HasNewIn,
    bool HasSale);

/// <summary>
/// Which scopes hold at least one listable product (NAV-3). Pairs are (gender code, other code). Built from the read model by one grouped
/// query per kind, never one query per menu entry.
/// </summary>
public sealed class MenuAvailability
{
    public IReadOnlySet<string> Genders { get; init; } = new HashSet<string>();

    public IReadOnlySet<(string Gender, string Category)> Categories { get; init; } = new HashSet<(string, string)>();

    public IReadOnlySet<(string Gender, string Collection)> Collections { get; init; } = new HashSet<(string, string)>();

    public IReadOnlySet<(string Gender, string Age)> Ages { get; init; } = new HashSet<(string, string)>();

    public IReadOnlySet<string> SaleGenders { get; init; } = new HashSet<string>();
}

/// <summary>
/// Builds the menu from lookup rows (owner decision 2026-10-25, Q-NAV-6): a new category, collection or age row appears with no code
/// change (NAV-5, NAV-9), and an entry with no listable products is not offered (NAV-3). Pure: no I/O.
/// </summary>
public static class MenuBuilder
{
    /// <summary>The URL segment for a lookup code: lower case, <c>_</c> as <c>-</c> (the same rule as a product path).</summary>
    public static string SegmentOf(string lookupCode) => lookupCode.Trim().ToLowerInvariant().Replace('_', '-');

    public static MenuTree Build(
        IEnumerable<MenuLookup> genders,
        IEnumerable<MenuLookup> categories,
        IEnumerable<MenuLookup> collections,
        IEnumerable<MenuLookup> ages,
        MenuAvailability availability,
        IReadOnlySet<string> hiddenGenderCodes)
    {
        var cats = Ordered(categories);
        var colls = Ordered(collections);
        var ageRows = Ordered(ages);

        var audiences = new List<MenuAudience>();
        foreach (var gender in Ordered(genders))
        {
            if (hiddenGenderCodes.Contains(gender.Code) || !availability.Genders.Contains(gender.Code))
            {
                continue;
            }

            var home = "/" + SegmentOf(gender.Code);
            var entries = new List<MenuEntry> { new(MenuEntryKind.NewIn, string.Empty, "New in", home + "/new-in") };
            if (availability.SaleGenders.Contains(gender.Code))
            {
                entries.Add(new MenuEntry(MenuEntryKind.Sale, string.Empty, "Sale", home + "/sale"));
            }

            entries.AddRange(cats
                .Where(c => availability.Categories.Contains((gender.Code, c.Code)))
                .Select(c => new MenuEntry(MenuEntryKind.Category, c.Code, c.Name, home + "/" + SegmentOf(c.Code))));
            entries.AddRange(colls
                .Where(c => availability.Collections.Contains((gender.Code, c.Code)))
                .Select(CollectionEntry));
            entries.AddRange(ageRows
                .Where(a => availability.Ages.Contains((gender.Code, a.Code)))
                .Select(a => new MenuEntry(MenuEntryKind.Age, a.Code, a.Name, home + "?age=" + SegmentOf(a.Code))));

            audiences.Add(new MenuAudience(gender.Code, gender.Name, home, entries));
        }

        var shared = colls
            .Where(c => availability.Collections.Any(p => p.Collection == c.Code))
            .Select(CollectionEntry)
            .ToList();

        return new MenuTree(audiences, shared, audiences.Count > 0, availability.SaleGenders.Count > 0);
    }

    private static MenuEntry CollectionEntry(MenuLookup c) =>
        new(MenuEntryKind.Collection, c.Code, c.Name, "/collections/" + SegmentOf(c.Code));

    /// <summary>Active rows only, in display order (a retired row is never offered), ties broken by code so the order is stable.</summary>
    private static List<MenuLookup> Ordered(IEnumerable<MenuLookup> rows) =>
        rows.Where(r => r.IsActive).OrderBy(r => r.SortSeq).ThenBy(r => r.Code, StringComparer.Ordinal).ToList();
}
