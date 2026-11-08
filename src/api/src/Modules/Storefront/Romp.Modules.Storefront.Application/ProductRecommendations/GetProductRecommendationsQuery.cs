using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.ProductPages;

namespace Romp.Modules.Storefront.Application.ProductRecommendations;

/// <summary>The two rows under a product: "You may also like" and "Trending now". Either may be empty, and the page then omits it (REC-10).</summary>
public sealed record ProductRecommendationsDto(IReadOnlyList<ListingTileDto> Similar, IReadOnlyList<ListingTileDto> Trending);

/// <summary>
/// The recommendations of a published product, by its path (REC-1, REC-4, REC-5). Reads only precomputed rows and the live read model, so a
/// product that was unpublished or sold out since the last refresh drops out at once. A failure in either row is logged and that row is left
/// empty: this query never throws to a product page (REC-10, NFR-FT-08). Null when the path is not a published product.
/// </summary>
public sealed record GetProductRecommendationsQuery(string? Path) : IRequest<ProductRecommendationsDto?>;

public sealed partial class GetProductRecommendationsQueryHandler(
    IStorefrontReadDbContext db, IListingSettings listing, IRecommendationSettings settings, TimeProvider clock, ILogger<GetProductRecommendationsQueryHandler> logger)
    : IRequestHandler<GetProductRecommendationsQuery, ProductRecommendationsDto?>
{
    public async Task<ProductRecommendationsDto?> Handle(GetProductRecommendationsQuery request, CancellationToken cancellationToken)
    {
        if (ProductPath.Normalise(request.Path) is not { } path)
        {
            return null;
        }

        var product = await db.Products.AsNoTracking()
            .Where(p => p.Path == path.Value)
            .Select(p => new { p.Id, p.GenderCode, p.AgeCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return null;
        }

        IReadOnlyList<long> similar = [];
        try
        {
            similar = await SimilarIdsAsync(product.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRowFailed(ex, "similar");
        }

        IReadOnlyList<long> trending = [];
        try
        {
            trending = await TrendingIdsAsync(product.Id, product.GenderCode, product.AgeCode, similar, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRowFailed(ex, "trending");
        }

        try
        {
            var context = new TileContext([], listing.LowStockThreshold, clock.GetUtcNow().AddDays(-listing.NewArrivalsDays));
            var tiles = (await ListingTiles.ByIdsAsync(db, [.. similar, .. trending], context, cancellationToken)).ToDictionary(t => t.ProductId);
            return new ProductRecommendationsDto(
                [.. similar.Where(tiles.ContainsKey).Select(id => tiles[id])],
                [.. trending.Where(tiles.ContainsKey).Select(id => tiles[id])]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogRowFailed(ex, "tiles");
            return new ProductRecommendationsDto([], []);
        }
    }

    /// <summary>The best precomputed rows that are still published and can still be bought, in rank order (REC-5).</summary>
    private async Task<IReadOnlyList<long>> SimilarIdsAsync(long productId, CancellationToken cancellationToken) =>
        await db.SimilarProducts.AsNoTracking()
            .Where(s => s.ProductId == productId)
            .Join(db.Products.Where(p => p.IsAvailable && p.IsListable), s => s.SimilarProductId, p => p.Id, (s, p) => new { s.Rank, p.Id })
            .OrderBy(x => x.Rank)
            .Select(x => x.Id)
            .Take(settings.RowSize)
            .ToListAsync(cancellationToken);

    /// <summary>Top sellers over the window: the same gender and age group first, then the whole shop to fill the row (REC-4).</summary>
    private async Task<IReadOnlyList<long>> TrendingIdsAsync(long productId, string gender, string age, IReadOnlyList<long> similar, CancellationToken cancellationToken)
    {
        var since = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(1 - settings.SalesWindowDays);
        var excluded = new List<long>(similar) { productId };

        var inGroup = await TopSellersAsync(since, p => p.IsAvailable && p.IsListable && p.GenderCode == gender && p.AgeCode == age && !excluded.Contains(p.Id), settings.RowSize, cancellationToken);
        if (inGroup.Count >= settings.RowSize)
        {
            return inGroup;
        }

        excluded.AddRange(inGroup);
        var rest = await TopSellersAsync(since, p => p.IsAvailable && p.IsListable && !excluded.Contains(p.Id), settings.RowSize - inGroup.Count, cancellationToken);
        return [.. inGroup, .. rest];
    }

    /// <summary>Product ids by units sold since <paramref name="since"/>, most first (ties: lowest id), among the products the filter allows; products with no sales are left out.</summary>
    private async Task<List<long>> TopSellersAsync(DateOnly since, System.Linq.Expressions.Expression<Func<Domain.PublishedCatalog.PublishedProduct, bool>> allowed, int take, CancellationToken cancellationToken) =>
        await (from s in db.ProductSaleDays.AsNoTracking()
               where s.Day >= since
               join p in db.Products.Where(allowed) on s.ProductId equals p.Id
               group s by s.ProductId into g
               select new { Id = g.Key, Units = g.Sum(x => x.Units) })
            .Where(x => x.Units > 0)
            .OrderByDescending(x => x.Units).ThenBy(x => x.Id)
            .Select(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);


    [LoggerMessage(Level = LogLevel.Warning, Message = "Recommendations row {Row} failed; it is left out of the page.")]
    private partial void LogRowFailed(Exception exception, string row);
}
