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

    /// <summary>Some variant can be bought. Listings put these before the sold-out products (SLD-2).</summary>
    public bool InStock { get; init; }

    public decimal? From { get; init; }

    public decimal? CompareAt { get; init; }

    public int Colours { get; init; }

    public bool HasSoldOutSizes { get; init; }

    public decimal MaxDiscountPercent { get; init; }
}

/// <summary>What the tile builder needs besides the rows: the colours the visitor filtered by, the low-stock threshold and the New In cutoff.</summary>
internal sealed record TileContext(IReadOnlyCollection<string> PreferredColours, int LowStockThreshold, DateTimeOffset NewCutoff);

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
            InStock = p.IsAvailable,
            From = matching.Where(v => v.ProductId == p.Id).Min(v => v.Price),
            CompareAt = matching.Where(v => v.ProductId == p.Id && v.CompareAtPrice > v.Price).OrderBy(v => v.Price).Select(v => v.CompareAtPrice).FirstOrDefault(),
            Colours = matching.Where(v => v.ProductId == p.Id).Select(v => v.ColourCode).Distinct().Count(),
            HasSoldOutSizes = db.Variants.Any(v => v.ProductId == p.Id && v.IsSellable && v.AvailableQuantity <= 0),
            MaxDiscountPercent = p.MaxDiscountPercent,
        });

    /// <summary>Attaches the pictures and the sizes (three queries) and builds the tiles in the order given.</summary>
    public static async Task<List<ListingTileDto>> BuildAsync(IStorefrontReadDbContext db, IReadOnlyList<TileRow> page, TileContext context, CancellationToken cancellationToken)
    {
        var pageIds = page.Select(x => x.Id).ToList();
        var images = await PicturesAsync(db, pageIds, withSecond: true, cancellationToken);
        var sizes = await SizesAsync(db, pageIds, context, cancellationToken);

        return page.Select(x =>
        {
            var tileSizes = sizes.GetValueOrDefault(x.Id) ?? [];
            images.TryGetValue(x.Id, out var pictures);
            return new ListingTileDto(
                x.Id,
                x.Path,
                x.Name,
                x.From,
                x.CompareAt,
                x.CompareAt is > 0 && x.From is { } now && x.CompareAt > now ? (int)Math.Round((x.CompareAt.Value - now) / x.CompareAt.Value * 100m) : 0,
                x.CompareAt is > 0 && x.From is { } was && x.CompareAt > was ? x.CompareAt.Value - was : 0m,
                x.Colours,
                x.HasSoldOutSizes,
                tileSizes,
                pictures?.Main,
                x.PublishedAt >= context.NewCutoff,
                tileSizes.Any(s => s.Availability == nameof(AvailabilityClass.LowStock)),
                pictures?.Second,
                !x.InStock);
        }).ToList();
    }

    /// <summary>
    /// Every size of each product on the page, in size order, in one query. A size is sold out when no sellable variant of it has stock. For
    /// the card's quick add each size also names the variant it would add: the one of the colour the card shows (the colour the visitor
    /// filtered by, else the colour of the cheapest variant), and when that colour lacks the size, the cheapest variant of that size that is in
    /// stock in another colour. The availability is the same classifier the product page uses (low stock threshold from settings).
    /// </summary>
    private static async Task<Dictionary<long, IReadOnlyList<ListingTileSizeDto>>> SizesAsync(IStorefrontReadDbContext db, List<long> productIds, TileContext context, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var variants = await db.Variants.AsNoTracking()
            .Where(v => productIds.Contains(v.ProductId) && v.IsSellable)
            .Select(v => new SizeVariant(v.Id, v.ProductId, v.ColourCode, v.ColourName, v.SizeCode, v.SizeName, v.SizeSeq, v.Price, v.IsSellable, v.AvailableQuantity))
            .ToListAsync(cancellationToken);

        return variants.GroupBy(v => v.ProductId).ToDictionary(
            g => g.Key,
            g =>
            {
                var defaultColour = DefaultColour(g, context.PreferredColours);
                return (IReadOnlyList<ListingTileSizeDto>)[.. g.GroupBy(v => new { v.SizeCode, v.SizeName, v.SizeSeq })
                    .OrderBy(s => s.Key.SizeSeq).ThenBy(s => s.Key.SizeCode, StringComparer.Ordinal)
                    .Select(s =>
                    {
                        var pick = Pick(s, defaultColour, context.LowStockThreshold);
                        return new ListingTileSizeDto(
                            s.Key.SizeCode,
                            s.Key.SizeName,
                            !s.Any(v => v.AvailableQuantity == null || v.AvailableQuantity > 0),
                            pick.Variant.Id,
                            Availability.Classify(pick.Variant.IsSellable, pick.Variant.AvailableQuantity, context.LowStockThreshold).ToString(),
                            pick.Variant.ColourName);
                    })];
            });
    }

    /// <summary>The colour the card shows: the first colour the visitor filtered by that the product has, else the colour of the cheapest variant.</summary>
    private static string DefaultColour(IEnumerable<SizeVariant> variants, IReadOnlyCollection<string> preferred)
    {
        var list = variants.ToList();
        static string Key(string code) => code.ToLowerInvariant().Replace('_', '-');
        var pool = preferred.Count > 0 ? list.Where(v => preferred.Contains(Key(v.ColourCode))).ToList() : list;
        if (pool.Count == 0)
        {
            pool = list;
        }

        return pool.OrderBy(v => v.Price ?? decimal.MaxValue).ThenBy(v => v.ColourCode, StringComparer.Ordinal).First().ColourCode;
    }

    private static (SizeVariant Variant, bool InDefaultColour) Pick(IEnumerable<SizeVariant> size, string defaultColour, int threshold)
    {
        var list = size.ToList();
        var ofDefault = list.FirstOrDefault(v => string.Equals(v.ColourCode, defaultColour, StringComparison.Ordinal));
        if (ofDefault is not null)
        {
            return (ofDefault, true);
        }

        var buyable = list.Where(v => Availability.Classify(v.IsSellable, v.AvailableQuantity, threshold).IsPurchasable());
        var cheapest = (buyable.Any() ? buyable : list).OrderBy(v => v.Price ?? decimal.MaxValue).ThenBy(v => v.Id).First();
        return (cheapest, false);
    }

    private sealed record SizeVariant(long Id, long ProductId, string ColourCode, string ColourName, string SizeCode, string SizeName, short SizeSeq, decimal? Price, bool IsSellable, int? AvailableQuantity);

    private sealed record Pictures(ListingImageDto? Main, ListingImageDto? Second);

    /// <summary>The main image of each product with the derivative files that exist: two queries however many products.</summary>
    internal static async Task<Dictionary<long, ListingImageDto>> ImagesAsync(IStorefrontReadDbContext db, List<long> productIds, CancellationToken cancellationToken)
    {
        var pictures = await PicturesAsync(db, productIds, withSecond: false, cancellationToken);
        return pictures.Where(p => p.Value.Main is not null).ToDictionary(p => p.Key, p => p.Value.Main!);
    }

    /// <summary>
    /// The main picture of each product and, when asked, its second: the next picture by order whose colour is none or the main picture's
    /// colour. One query for the picture rows and one for the derivative files, however many products.
    /// </summary>
    private static async Task<Dictionary<long, Pictures>> PicturesAsync(IStorefrontReadDbContext db, List<long> productIds, bool withSecond, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        var rows = await db.Images.AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId) && (withSecond || i.IsMain))
            .ToListAsync(cancellationToken);

        var chosen = new Dictionary<long, (ImageRow Main, ImageRow? Second)>();
        foreach (var group in rows.GroupBy(i => i.ProductId))
        {
            var main = group.FirstOrDefault(i => i.IsMain);
            if (main is null)
            {
                continue;
            }

            var second = withSecond
                ? group.Where(i => !i.IsMain && (i.ColourId is null || i.ColourId == main.ColourId)).OrderBy(i => i.SortSeq).ThenBy(i => i.Id).FirstOrDefault()
                : null;
            chosen[group.Key] = (main, second);
        }

        var imageIds = chosen.Values.SelectMany(c => c.Second is null ? new[] { c.Main.Id } : [c.Main.Id, c.Second.Id]).ToList();
        var files = await db.Derivatives.AsNoTracking()
            .Where(d => imageIds.Contains(d.ImageId) && d.Status == DerivativeStatus.Done && d.FileKey != null)
            .ToListAsync(cancellationToken);

        ListingImageDto Dto(ImageRow i) => new(
            i.AltText,
            i.Width,
            i.Height,
            files.Where(d => d.ImageId == i.Id).GroupBy(d => d.Width).OrderBy(g => g.Key)
                .Select(g => new ListingImageSourceDto(
                    g.Key,
                    "/media/" + (g.FirstOrDefault(d => d.Format == DerivativeFormat.Webp)?.FileKey ?? string.Empty),
                    g.FirstOrDefault(d => d.Format == DerivativeFormat.Jpeg) is { } jpeg ? "/media/" + jpeg.FileKey : null))
                .Where(s => s.Webp != "/media/")
                .ToList());

        return chosen.ToDictionary(
            c => c.Key,
            c =>
            {
                var second = c.Value.Second is null ? null : Dto(c.Value.Second);
                return new Pictures(Dto(c.Value.Main), second is { Sources.Count: > 0 } ? second : null);
            });
    }
}
