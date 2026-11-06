using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain;

namespace Romp.Modules.Storefront.Application.CollectionsPage;

/// <summary>
/// The collections index (COL-1..3, 6): a page of the enabled collections that have at least one listable product. Page and page size are
/// clamped, never an error.
/// </summary>
public sealed record GetCollectionsQuery(int Page = 1, int PageSize = CollectionsPaging.DefaultPageSize) : IRequest<CollectionsPageDto>;

/// <summary>One tile: the collection, how many listable pieces it holds and the main picture of the newest of them (null when it has none yet).</summary>
public sealed record CollectionTileDto(string Code, string Name, string Path, int Pieces, ListingImageDto? Image);

public sealed record CollectionsPageDto(IReadOnlyList<CollectionTileDto> Items, int Total, int Page, int PageSize, int TotalPages) : IPublicStorefrontResponse;

public static class CollectionsPaging
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 48;
}

public sealed class GetCollectionsQueryHandler(
    IStorefrontReadDbContext db,
    ICatalogLookupQueries lookups,
    ICatalogLookupCache cache) : IRequestHandler<GetCollectionsQuery, CollectionsPageDto>
{
    public async Task<CollectionsPageDto> Handle(GetCollectionsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, CollectionsPaging.MaxPageSize);
        var rows = await cache.GetOrLoadAsync(ct => CatalogLookups.LoadAsync(lookups, ct), cancellationToken);

        // One grouped query: per collection, how many listable products it holds and which is the newest.
        var held = await db.Collections.AsNoTracking()
            .Join(db.Products.Where(p => p.IsListable), c => c.ProductId, p => p.Id, (c, p) => new { c.CollectionCode, p.Id, p.PublishedAt })
            .GroupBy(x => x.CollectionCode)
            .Select(g => new
            {
                Code = g.Key,
                Pieces = g.Count(),
                NewestProductId = g.OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id).Select(x => x.Id).First(),
            })
            .ToListAsync(cancellationToken);

        // Only enabled lookup rows, in the order the owner set (sort, then name), then paged here: a shop has tens of collections.
        var listed = rows.Collections
            .Where(c => c.IsActive)
            .Join(held, c => c.Code, h => h.Code, (c, h) => (Lookup: c, Held: h))
            .OrderBy(x => x.Lookup.SortSeq).ThenBy(x => x.Lookup.Name, StringComparer.Ordinal)
            .ToList();

        var total = listed.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Clamp(request.Page, 1, int.MaxValue);
        var chosen = listed.Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue)).Take(pageSize).ToList();

        // One call for every picture on the page (two queries however many tiles).
        var images = await ListingTiles.ImagesAsync(db, [.. chosen.Select(x => x.Held.NewestProductId)], cancellationToken);

        var items = chosen.Select(x => new CollectionTileDto(
            x.Lookup.Code,
            x.Lookup.Name,
            "/collections/" + CodeSegment.Of(x.Lookup.Code),
            x.Held.Pieces,
            images.GetValueOrDefault(x.Held.NewestProductId) is { Sources.Count: > 0 } image ? image : null)).ToList();

        return new CollectionsPageDto(items, total, Math.Min(page, int.MaxValue), pageSize, totalPages);
    }
}
