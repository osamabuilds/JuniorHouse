using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Application.PublishedCatalog;

/// <summary>What the header of an image says, read without decoding the pixels.</summary>
public sealed record ImageProbe(int Width, int Height);

/// <summary>The original is not an image the storefront will process: unreadable, an unsupported type, or above the pixel cap (PCT-7). <c>Code</c> is the stable reason stored on the derivative.</summary>
public sealed class ImageRejectedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// The storefront's port onto an image library (published-catalog plan, PCT-7). Both calls take the original's bytes and refuse anything
/// above <c>maxPixels</c> before the pixels are decoded, so an image bomb costs a header read, not memory. Output carries no metadata
/// (no EXIF, location or colour-profile text) and is oriented upright.
/// </summary>
public interface IImageResizer
{
    /// <summary>Reads the dimensions from the header. Throws <see cref="ImageRejectedException"/> for a non-image or a picture above the cap.</summary>
    ImageProbe Probe(ReadOnlySpan<byte> original, long maxPixels);

    /// <summary>
    /// Scales the image to <paramref name="width"/> pixels wide (keeping the proportions; never enlarged past the original) and encodes it.
    /// <paramref name="quality"/> is 1 to 100.
    /// </summary>
    byte[] Resize(ReadOnlySpan<byte> original, int width, DerivativeFormat format, int quality, long maxPixels);
}
