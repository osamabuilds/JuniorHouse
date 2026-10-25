using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Application.PublishedCatalog;

/// <summary>The configuration the projection needs, behind a port so Application never reads configuration itself (Infrastructure binds <c>Storefront:*</c>).</summary>
public interface IReadModelSettings
{
    /// <summary>At or below this many units a variant reads "only a few left" (<see cref="Availability.Classify"/>).</summary>
    int LowStockThreshold { get; }

    /// <summary>The widths every image is resized to, once per format (<see cref="ReadModelProjector.Formats"/>).</summary>
    IReadOnlyList<int> ImageWidths { get; }

    /// <summary>An original with more pixels than this (width × height) is refused before it is decoded (PCT-7).</summary>
    long MaxImagePixels { get; }

    /// <summary>Encoder quality, 1 to 100, for every derivative.</summary>
    int ImageQuality { get; }

    /// <summary>Attempts a derivative gets before it is marked failed.</summary>
    int MaxDerivativeAttempts { get; }

    /// <summary>Wait after the first failed attempt; doubled for each further one.</summary>
    TimeSpan DerivativeRetryDelay { get; }

    /// <summary>How long a derivative file nothing references any more is kept (pages and CDNs may still hold its URL) before the sweep removes it.</summary>
    TimeSpan DerivativeCacheLifetime { get; }
}

/// <summary>
/// The derived half of a product row: the price range, the availability flags and whether the main image is ready. One function over
/// the rows already loaded, so the snapshot, stock and image-worker paths all compute it identically and in a constant number of queries.
/// </summary>
public static class ReadModelProjector
{
    public static readonly IReadOnlyList<DerivativeFormat> Formats = [DerivativeFormat.Webp, DerivativeFormat.Jpeg];

    /// <summary>The main image is the one with the lowest sort order (ties: the lowest id).</summary>
    public static ImageRow? MainImage(IEnumerable<ImageRow> images) =>
        images.OrderBy(i => i.SortSeq).ThenBy(i => i.Id).FirstOrDefault();

    public static AvailabilityClass ClassOf(VariantRow variant, int lowStockThreshold) =>
        Availability.Classify(variant.IsSellable, variant.AvailableQuantity, lowStockThreshold);

    /// <summary>
    /// Recomputes the derived fields of <paramref name="product"/> from its variants, its images and their derivatives. The main image is
    /// ready when every width × format derivative of it exists (<see cref="DerivativeStatus.Done"/>, PCT-7).
    /// </summary>
    public static void Recompute(
        PublishedProduct product,
        IReadOnlyCollection<VariantRow> variants,
        IReadOnlyCollection<ImageRow> images,
        IReadOnlyCollection<DerivativeRow> derivatives,
        IReadModelSettings settings)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(settings);

        var classes = variants
            .Select(v => (Variant: v, Class: ClassOf(v, settings.LowStockThreshold)))
            .ToList();
        var price = PriceSummary.Of(classes.Select(c => new PricedVariant(c.Variant.Price, c.Variant.CompareAtPrice, c.Class)));
        var main = MainImage(images);
        var imageReady = main is not null && IsReady(main.Id, derivatives, settings.ImageWidths);
        product.SetDerived(price, ProductVisibility.Evaluate(classes.Select(c => c.Class), imageReady), imageReady);
    }

    private static bool IsReady(long imageId, IReadOnlyCollection<DerivativeRow> derivatives, IReadOnlyList<int> widths)
    {
        var done = derivatives.Where(d => d.ImageId == imageId && d.Status == DerivativeStatus.Done)
            .Select(d => (d.Width, d.Format))
            .ToHashSet();
        return widths.Count > 0 && widths.All(w => Formats.All(f => done.Contains((w, f))));
    }

    /// <summary>Loads everything <see cref="Recompute"/> reads for one product and recomputes it; for paths that hold only the product.</summary>
    public static async Task RecomputeAsync(
        IStorefrontReadDbContext db, PublishedProduct product, IReadModelSettings settings, CancellationToken cancellationToken)
    {
        var variants = await db.Variants.Where(v => v.ProductId == product.Id).ToListAsync(cancellationToken);
        var images = await db.Images.Where(i => i.ProductId == product.Id).ToListAsync(cancellationToken);
        var imageIds = images.Select(i => i.Id).ToList();
        var derivatives = await db.Derivatives.Where(d => imageIds.Contains(d.ImageId)).ToListAsync(cancellationToken);
        Recompute(product, variants, images, derivatives, settings);
    }
}
