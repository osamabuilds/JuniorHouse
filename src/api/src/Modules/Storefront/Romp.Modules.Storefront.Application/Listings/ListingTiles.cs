using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Application.Listings;

/// <summary>One product row as the tiles need it, before pictures and sizes are attached.</summary>
internal sealed class TileRow
{
    public long Id { get; init; }

    public string Path { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public DateTimeOffset PublishedAt { get; init; }

    public decimal? From { get; init; }

    public decimal? CompareAt { get; init; }

    public int Colours { get; init; }

    public bool SoldOut { get; init; }

    public decimal MaxDiscountPercent { get; init; }
}

/// <summary>
/// Turns product rows into <see cref="ListingTileDto"/>s. Listings and search share it so a tile looks and prices the same wherever it
/// appears (LST-12): the price is the lowest among the variants that match the filter, and the pictures and sizes of a whole page cost two
/// queries however many products it holds.
/// </summary>
internal static class ListingTiles
{
    /// <summary>The tile columns of <paramref name="products"/>, computed in SQL against the variants that match the filter.</summary>
    public static IQueryable<TileRow> Project(IStorefrontReadDbContext db, IQueryable<PublishedProduct> products, IQueryable<VariantRow> matching) =>
        products.Select(p => new TileRow
        {
            Id = p.Id,
            Path = p.Path,
            Name = p.Name,
            PublishedAt = p.PublishedAt,
            From = matching.Where(v => v.ProductId == p.Id).Min(v => v.Price),
            CompareAt = matching.Where(v => v.ProductId == p.Id && v.CompareAtPrice > v.Price).OrderBy(v => v.Price).Select(v => v.CompareAtPrice).FirstOrDefault(),
            Colours = matching.Where(v => v.ProductId == p.Id).Select(v => v.ColourCode).Distinct().Count(),
            SoldOut = db.Variants.Any(v => v.ProductId == p.Id && v.IsSellable && v.AvailableQuantity <= 0),
            MaxDiscountPercent = p.MaxDiscountPercent,
        });

    /// <summary>Attaches the main picture and the sizes (two queries) and builds the tiles in the order given.</summary>
    public static async Task<List<ListingTileDto>> BuildAsync(IStorefrontReadDbContext db, IReadOnlyList<TileRow> page, CancellationToken cancellationToken)
    {
        var pageIds = page.Select(x => x.Id).ToList();
        var images = await ImagesAsync(db, pageIds, cancellationToken);
        var sizes = await SizesAsync(db, pageIds, cancellationToken);

        return page.Select(x => new ListingTileDto(
            x.Path,
            x.Name,
            x.From,
            x.CompareAt,
            x.CompareAt is > 0 && x.From is { } now && x.CompareAt > now ? (int)Math.Round((x.CompareAt.Value - now) / x.CompareAt.Value * 100m) : 0,
            x.CompareAt is > 0 && x.From is { } was && x.CompareAt > was ? x.CompareAt.Value - was : 0m,
            x.Colours,
            x.SoldOut,
            sizes.GetValueOrDefault(x.Id) ?? [],
            images.GetValueOrDefault(x.Id))).ToList();
    }

    /// <summary>Every size of each product on the page, in size order, in one query. A size is sold out when no sellable variant of it has stock.</summary>
    private static async Task<Dictionary<long, IReadOnlyList<ListingTileSizeDto>>> SizesAsync(IStorefrontReadDbContext db, List<long> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var variants = await db.Variants.AsNoTracking()
            .Where(v => productIds.Contains(v.ProductId) && v.IsSellable)
            .Select(v => new { v.ProductId, v.SizeCode, v.SizeName, v.SizeSeq, InStock = v.AvailableQuantity == null || v.AvailableQuantity > 0 })
            .ToListAsync(cancellationToken);

        return variants.GroupBy(v => v.ProductId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ListingTileSizeDto>)[.. g.GroupBy(v => new { v.SizeCode, v.SizeName, v.SizeSeq })
                .OrderBy(s => s.Key.SizeSeq).ThenBy(s => s.Key.SizeCode, StringComparer.Ordinal)
                .Select(s => new ListingTileSizeDto(s.Key.SizeCode, s.Key.SizeName, !s.Any(v => v.InStock)))]);
    }

    /// <summary>The main image of each product with the derivative files that exist: two queries however many products.</summary>
    private static async Task<Dictionary<long, ListingImageDto>> ImagesAsync(IStorefrontReadDbContext db, List<long> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var mains = await db.Images.AsNoTracking().Where(i => productIds.Contains(i.ProductId) && i.IsMain).ToListAsync(cancellationToken);
        var imageIds = mains.Select(i => i.Id).ToList();
        var files = await db.Derivatives.AsNoTracking()
            .Where(d => imageIds.Contains(d.ImageId) && d.Status == DerivativeStatus.Done && d.FileKey != null)
            .ToListAsync(cancellationToken);

        return mains.ToDictionary(
            i => i.ProductId,
            i => new ListingImageDto(
                i.AltText,
                i.Width,
                i.Height,
                files.Where(d => d.ImageId == i.Id).GroupBy(d => d.Width).OrderBy(g => g.Key)
                    .Select(g => new ListingImageSourceDto(
                        g.Key,
                        "/media/" + (g.FirstOrDefault(d => d.Format == DerivativeFormat.Webp)?.FileKey ?? string.Empty),
                        g.FirstOrDefault(d => d.Format == DerivativeFormat.Jpeg) is { } jpeg ? "/media/" + jpeg.FileKey : null))
                    .Where(s => s.Webp != "/media/")
                    .ToList()));
    }
}
