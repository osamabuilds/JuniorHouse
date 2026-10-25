using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using SkiaSharp;

namespace Romp.Modules.Storefront.Infrastructure.PublishedCatalog;

/// <summary>
/// <see cref="IImageResizer"/> over SkiaSharp. The type is sniffed from the bytes (never from a file name), the header is read first so the
/// pixel cap is enforced before decoding, and the output is a fresh encode, which drops all metadata. Transparent pixels are flattened onto
/// white for JPEG. Only the upright orientations are applied from EXIF (rotations); mirrored ones are shown as stored.
/// </summary>
public sealed class SkiaImageResizer : IImageResizer
{
    public ImageProbe Probe(ReadOnlySpan<byte> original, long maxPixels)
    {
        using var data = SKData.CreateCopy(original);
        using var codec = OpenCodec(data, maxPixels);
        var size = Upright(codec.EncodedOrigin, codec.Info.Width, codec.Info.Height);
        return new ImageProbe(size.Width, size.Height);
    }

    public byte[] Resize(ReadOnlySpan<byte> original, int width, DerivativeFormat format, int quality, long maxPixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, DerivativeName.MinWidth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, DerivativeName.MaxWidth);
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);

        using var data = SKData.CreateCopy(original);
        using var codec = OpenCodec(data, maxPixels);
        var origin = codec.EncodedOrigin;
        using var decoded = SKBitmap.Decode(codec) ?? throw new ImageRejectedException("DECODE_FAILED", "The image could not be decoded.");
        using var upright = Orient(decoded, origin);

        var targetWidth = Math.Min(width, upright.Width);
        var targetHeight = Math.Max(1, (int)Math.Round((double)upright.Height * targetWidth / upright.Width));
        var info = new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        if (format == DerivativeFormat.Jpeg)
        {
            canvas.Clear(SKColors.White);
        }

        using var image = SKImage.FromBitmap(upright);
        canvas.DrawImage(image, new SKRect(0, 0, targetWidth, targetHeight), new SKSamplingOptions(SKCubicResampler.Mitchell));

        using var snapshot = surface.Snapshot();
        var encoded = format == DerivativeFormat.Webp
            ? snapshot.Encode(SKEncodedImageFormat.Webp, quality)
            : snapshot.Encode(SKEncodedImageFormat.Jpeg, quality);
        return encoded?.ToArray() ?? throw new ImageRejectedException("ENCODE_FAILED", "The image could not be encoded.");
    }

    private static SKCodec OpenCodec(SKData data, long maxPixels)
    {
        var codec = SKCodec.Create(data) ?? throw new ImageRejectedException("NOT_AN_IMAGE", "The file is not an image the storefront can read.");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
        {
            var type = codec.EncodedFormat;
            codec.Dispose();
            throw new ImageRejectedException("UNSUPPORTED_TYPE", $"Images of type {type} are not processed.");
        }

        if ((long)codec.Info.Width * codec.Info.Height > maxPixels)
        {
            codec.Dispose();
            throw new ImageRejectedException("TOO_LARGE", "The image has more pixels than the storefront accepts.");
        }

        return codec;
    }

    private static (int Width, int Height) Upright(SKEncodedOrigin origin, int width, int height) =>
        origin is SKEncodedOrigin.RightTop or SKEncodedOrigin.LeftBottom ? (height, width) : (width, height);

    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is not (SKEncodedOrigin.RightTop or SKEncodedOrigin.LeftBottom or SKEncodedOrigin.BottomRight))
        {
            return source.Copy();
        }

        var (width, height) = Upright(origin, source.Width, source.Height);
        var rotated = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(rotated);
        switch (origin)
        {
            case SKEncodedOrigin.RightTop: // stored sideways, 90 degrees clockwise to display
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.LeftBottom: // 270 degrees clockwise
                canvas.Translate(0, height);
                canvas.RotateDegrees(270);
                break;
            default: // BottomRight: upside down
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        return rotated;
    }
}
