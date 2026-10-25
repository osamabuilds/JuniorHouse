using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Domain.Search;

namespace Romp.Modules.Storefront.Application.Search;

/// <summary>
/// A search results page: the products that match the typed text, narrowed by the same filters as a listing and sorted by relevance
/// unless the visitor chose a price sort (SRC-1, 2, 3, 8, 14).
/// </summary>
public sealed record SearchProductsQuery(string? Text, ListingFilter Filter) : IRequest<SearchProductsResult>;

/// <summary>
/// The results page. <c>Query</c> is the normalised text, safe to show once encoded. <c>Options</c> are the filter choices among the matches.
/// With no match <c>Total</c> is 0 and the page shows its empty state.
/// </summary>
public sealed record SearchResultDto(
    string Query,
    IReadOnlyList<ListingTileDto> Tiles,
    int Total,
    int Page,
    int PageSize,
    int TotalPages,
    int MaxSavingPercent,
    ListingOptionsDto Options) : IPublicStorefrontResponse;

public enum SearchOutcome
{
    Found = 1,

    /// <summary>Nothing matched, or nothing was typed. A normal answer (200 with noindex), not an error (SRC-2).</summary>
    NoResults = 2,

    /// <summary>A page after the last one.</summary>
    PastTheEnd = 3,
}

public sealed record SearchProductsResult(SearchOutcome Outcome, SearchResultDto Page);

public sealed class SearchProductsQueryHandler(
    IStorefrontReadDbContext db,
    IProductSearchIndex index,
    ISynonymCache synonyms,
    ISearchSettings settings,
    ICatalogLookupQueries lookups,
    ICatalogLookupCache lookupCache) : IRequestHandler<SearchProductsQuery, SearchProductsResult>
{
    public async Task<SearchProductsResult> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var query = SearchQuery.Parse(request.Text);
        var empty = new SearchProductsResult(SearchOutcome.NoResults, EmptyPage(query.Text, filter));
        if (query.IsEmpty)
        {
            return empty;
        }

        var expansion = await synonyms.GetOrLoadAsync(LoadSynonymsAsync, cancellationToken);
        var tsQuery = TsQueryText.From(expansion.Expand(query));
        if (tsQuery is null)
        {
            return empty;
        }

        var hits = new List<SearchHit>(await index.FullTextAsync(tsQuery, settings.MaxHits, cancellationToken));
        if (hits.Count < settings.MinFullTextHits)
        {
            var known = hits.Select(h => h.ProductId).ToHashSet();
            var similar = await index.SimilarAsync(query.Text, settings.SimilarityThreshold, settings.MaxHits, cancellationToken);
            // A near spelling ranks below any exact word match, whatever its similarity.
            hits.AddRange(similar.Where(h => known.Add(h.ProductId)).Select(h => h with { Rank = h.Rank * 0.001 }));
        }

        if (hits.Count == 0)
        {
            return empty;
        }

        var ranks = hits.Take(settings.MaxHits).ToDictionary(h => h.ProductId, h => h.Rank);
        var ids = ranks.Keys.ToList();
        var matching = ListingQueryBuilder.MatchingVariants(db, filter);
        var products = ListingQueryBuilder.Matching(db.Products.AsNoTracking().Where(p => p.IsListable && ids.Contains(p.Id)), matching, filter);

        // At most MaxHits small rows: sorting and paging them here costs nothing and keeps the relevance order the database computed.
        var rows = await ListingTiles.Project(db, products, matching).ToListAsync(cancellationToken);
        var ordered = filter.Sort switch
        {
            ListingSort.PriceAscending => rows.OrderBy(x => x.From).ThenBy(x => x.Id),
            ListingSort.PriceDescending => rows.OrderByDescending(x => x.From).ThenBy(x => x.Id),
            _ => rows.OrderByDescending(x => ranks[x.Id]).ThenByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id),
        };

        var total = rows.Count;
        if (total == 0)
        {
            return empty;
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)filter.PageSize));
        if (filter.Page > totalPages)
        {
            return new SearchProductsResult(SearchOutcome.PastTheEnd, EmptyPage(query.Text, filter));
        }

        var page = ordered.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        var tiles = await ListingTiles.BuildAsync(db, page, cancellationToken);

        var lookupRows = await lookupCache.GetOrLoadAsync(ct => CatalogLookups.LoadAsync(lookups, ct), cancellationToken);
        var scopeProducts = db.Products.AsNoTracking().Where(p => p.IsListable && ids.Contains(p.Id));
        var scopeVariants = ListingQueryBuilder.MatchingVariants(db, new ListingFilter()).Where(v => ids.Contains(v.ProductId));
        var options = await ListingOptionsBuilder.BuildAsync(scopeVariants, scopeProducts, lookupRows, cancellationToken);

        return new SearchProductsResult(
            SearchOutcome.Found,
            new SearchResultDto(query.Text, tiles, total, filter.Page, filter.PageSize, totalPages, tiles.Count == 0 ? 0 : tiles.Max(t => t.SavingPercent), options));
    }

    private async Task<SynonymExpansion> LoadSynonymsAsync(CancellationToken cancellationToken)
    {
        var terms = await (
            from t in db.SynonymTerms.AsNoTracking()
            join g in db.SynonymGroups.AsNoTracking() on t.GroupId equals g.Id
            where g.IsActive
            select new { t.GroupId, t.Term }).ToListAsync(cancellationToken);

        return new SynonymExpansion(terms.GroupBy(t => t.GroupId).Select(g => (IReadOnlyCollection<string>)g.Select(t => t.Term).ToList()));
    }

    private static SearchResultDto EmptyPage(string query, ListingFilter filter) =>
        new(query, [], 0, filter.Page, filter.PageSize, 1, 0, new ListingOptionsDto([], [], null, null, []));
}
