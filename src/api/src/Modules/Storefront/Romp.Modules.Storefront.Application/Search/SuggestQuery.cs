using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain;
using Romp.Modules.Storefront.Domain.Search;

namespace Romp.Modules.Storefront.Application.Search;

/// <summary>Autosuggest: up to <see cref="ISearchSettings.SuggestLimit"/> products and categories for the text typed so far (SRC-1, 7, 14).</summary>
public sealed record SuggestQuery(string? Text) : IRequest<SuggestResultDto>;

/// <summary><c>Kind</c> is <c>product</c> or <c>category</c>. <c>Image</c> and <c>Price</c> (the lowest price, PKR) are set for products only.</summary>
public sealed record SuggestionItemDto(string Kind, string Label, string Path, ListingImageDto? Image, decimal? Price = null);

public sealed record SuggestResultDto(string Query, IReadOnlyList<SuggestionItemDto> Items) : IPublicStorefrontResponse;

/// <summary>A short-lived answer per normalised prefix, bounded in size, so typing the same letters again costs no query (search plan, Endpoints).</summary>
public interface ISuggestCache
{
    Task<SuggestResultDto> GetOrAddAsync(string normalisedPrefix, Func<CancellationToken, Task<SuggestResultDto>> create, CancellationToken cancellationToken);
}

public sealed class SuggestQueryHandler(
    IStorefrontReadDbContext db,
    IProductSearchIndex index,
    ISynonymCache synonyms,
    ISearchSettings settings,
    ISuggestCache cache,
    ICatalogLookupQueries lookups,
    ICatalogLookupCache lookupCache) : IRequestHandler<SuggestQuery, SuggestResultDto>
{
    private const int MaxCategories = 3;

    public async Task<SuggestResultDto> Handle(SuggestQuery request, CancellationToken cancellationToken)
    {
        var query = SearchQuery.Parse(request.Text);
        if (query.IsEmpty)
        {
            return new SuggestResultDto(query.Text, []);
        }

        return await cache.GetOrAddAsync(query.Text, ct => BuildAsync(query, ct), cancellationToken);
    }

    private async Task<SuggestResultDto> BuildAsync(SearchQuery query, CancellationToken cancellationToken)
    {
        var categories = await CategoriesAsync(query, cancellationToken);
        var productSlots = Math.Max(0, settings.SuggestLimit - categories.Count);

        var expansion = await synonyms.GetOrLoadAsync(ct => SynonymLoader.LoadAsync(db, ct), cancellationToken);
        var tsQuery = TsQueryText.From(expansion.Expand(query, lastWordIsPartial: true));
        var items = new List<SuggestionItemDto>(categories);

        if (tsQuery is not null && productSlots > 0)
        {
            var hits = await index.FullTextAsync(tsQuery, productSlots, cancellationToken);
            if (hits.Count == 0)
            {
                hits = await index.SimilarAsync(query.Text, settings.SimilarityThreshold, productSlots, cancellationToken);
            }

            var ids = hits.Select(h => h.ProductId).ToList();
            var products = (await db.Products.AsNoTracking().Where(p => p.IsListable && ids.Contains(p.Id)).Select(p => new { p.Id, p.Name, p.Path, p.MinPrice }).ToListAsync(cancellationToken))
                .ToDictionary(p => p.Id);
            var images = await ListingTiles.ImagesAsync(db, ids, cancellationToken);
            items.AddRange(ids.Where(products.ContainsKey).Select(id => new SuggestionItemDto("product", products[id].Name, products[id].Path, images.GetValueOrDefault(id), products[id].MinPrice)));
        }

        return new SuggestResultDto(query.Text, items);
    }

    /// <summary>Categories whose name starts with every word typed, for each audience that has a listable product in them.</summary>
    private async Task<List<SuggestionItemDto>> CategoriesAsync(SearchQuery query, CancellationToken cancellationToken)
    {
        var rows = await lookupCache.GetOrLoadAsync(ct => CatalogLookups.LoadAsync(lookups, ct), cancellationToken);
        var codes = rows.Categories
            .Where(c => c.IsActive && StartsWithEveryWord(c.Name, query.Tokens))
            .Select(c => c.Code)
            .ToList();
        if (codes.Count == 0)
        {
            return [];
        }

        var pairs = await db.Products.AsNoTracking()
            .Where(p => p.IsListable && codes.Contains(p.CategoryCode))
            .Select(p => new { p.GenderCode, p.GenderName, p.CategoryCode, p.CategoryName })
            .Distinct()
            .OrderBy(p => p.CategoryName).ThenBy(p => p.GenderName)
            .Take(MaxCategories)
            .ToListAsync(cancellationToken);

        return pairs.Select(p => new SuggestionItemDto(
            "category",
            $"{p.CategoryName} · {p.GenderName}",
            "/" + CodeSegment.Of(p.GenderCode) + "/" + CodeSegment.Of(p.CategoryCode),
            null)).ToList();
    }

    private static bool StartsWithEveryWord(string name, IReadOnlyList<string> tokens)
    {
        var words = SearchQuery.Parse(name).Tokens;
        return tokens.All(t => words.Any(w => w.StartsWith(t, StringComparison.Ordinal)));
    }
}
