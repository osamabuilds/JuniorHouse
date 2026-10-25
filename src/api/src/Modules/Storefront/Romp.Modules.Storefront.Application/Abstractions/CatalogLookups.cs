using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Domain;

namespace Romp.Modules.Storefront.Application.Abstractions;

/// <summary>The four lookup lists the menu and the listings are built from.</summary>
public sealed record CatalogLookups(
    IReadOnlyList<LookupRow> Genders,
    IReadOnlyList<LookupRow> Categories,
    IReadOnlyList<LookupRow> Collections,
    IReadOnlyList<LookupRow> Ages)
{
    /// <summary>Reads all four lists from Reference through its contract: four queries, none per row.</summary>
    public static async Task<CatalogLookups> LoadAsync(ICatalogLookupQueries lookups, CancellationToken cancellationToken)
    {
        // Sequential: the lookups share one Reference context per scope.
        var genders = await lookups.ListGendersAsync(cancellationToken);
        var categories = await lookups.ListCategoriesAsync(cancellationToken);
        var collections = await lookups.ListCollectionsAsync(cancellationToken);
        var ages = await lookups.ListAgeBracketsAsync(cancellationToken);
        return new CatalogLookups(Map(genders), Map(categories), Map(collections), Map(ages));
    }

    private static List<LookupRow> Map(IReadOnlyList<CatalogLookupItem> items) =>
        items.Select(i => new LookupRow(i.Code, i.Name, i.SortSeq, i.IsActive)).ToList();
}

/// <summary>
/// The 60-second, single-flight copy of the lookups (plan: a cache stampede must not turn into a burst of Reference queries). One entry per
/// process; a concurrent caller waits for the load already running.
/// </summary>
public interface ICatalogLookupCache
{
    Task<CatalogLookups> GetOrLoadAsync(Func<CancellationToken, Task<CatalogLookups>> load, CancellationToken cancellationToken);
}
