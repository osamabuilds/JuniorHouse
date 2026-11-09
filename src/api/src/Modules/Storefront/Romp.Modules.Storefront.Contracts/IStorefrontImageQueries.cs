namespace Romp.Modules.Storefront.Contracts;

/// <summary>
/// A small picture of a catalogue image, ready to show: the WebP file and its JPEG fallback at the same width. Both are site-relative
/// <c>/media/...</c> paths the browser can load directly.
/// </summary>
public sealed record ImageThumbnail(long ImageId, string WebpUrl, string? JpegUrl, int Width);

/// <summary>
/// Lets another module (the basket, later the order pages) show a product picture without reading Storefront's tables (ADR 0002). One bulk
/// call for any number of images, never one call per picture. An image with no finished file yet is simply absent, and the caller shows its
/// placeholder.
/// </summary>
public interface IStorefrontImageQueries
{
    /// <summary>The most images one call accepts.</summary>
    public const int MaxIds = 100;

    /// <summary>For each image, the smallest finished resize that is at least <paramref name="minWidth"/> wide (else the widest there is).</summary>
    Task<IReadOnlyList<ImageThumbnail>> GetThumbnailsAsync(IReadOnlyCollection<long> imageIds, int minWidth, CancellationToken cancellationToken);
}
