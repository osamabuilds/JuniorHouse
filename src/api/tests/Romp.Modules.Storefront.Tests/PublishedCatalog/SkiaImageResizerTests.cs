using System.Text;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.PublishedCatalog;
using SkiaSharp;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog tasks 16-17: the real SkiaSharp resizer on generated pictures - sizes, formats, the pixel cap, non-images and stripped metadata (PCT-7).</summary>
public sealed class SkiaImageResizerTests
{
    private const long Cap = 40_000_000;
    private readonly SkiaImageResizer _resizer = new();

    private static byte[] Picture(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png, SKColor? colour = null)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(colour ?? new SKColor(200, 40, 40));
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(format, 90).ToArray();
    }

    private static SKCodec Open(byte[] bytes) => SKCodec.Create(SKData.CreateCopy(bytes))!;

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void Probe_ReadsTheDimensionsFromTheHeader()
    {
        var probe = _resizer.Probe(Picture(300, 400), Cap);

        Assert.Equal(new ImageProbe(300, 400), probe);
    }

    [Theory]
    [Trait("Spec", "PCT-7")]
    [InlineData(DerivativeFormat.Webp, SKEncodedImageFormat.Webp)]
    [InlineData(DerivativeFormat.Jpeg, SKEncodedImageFormat.Jpeg)]
    public void Resize_ScalesToTheWidthKeepingProportions_InTheRequestedFormat(DerivativeFormat format, SKEncodedImageFormat expected)
    {
        var output = _resizer.Resize(Picture(800, 1200), 320, format, 80, Cap);

        using var codec = Open(output);
        Assert.Equal(expected, codec.EncodedFormat);
        Assert.Equal(320, codec.Info.Width);
        Assert.Equal(480, codec.Info.Height);
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void Resize_NeverEnlargesPastTheOriginal()
    {
        var output = _resizer.Resize(Picture(200, 100), 640, DerivativeFormat.Webp, 80, Cap);

        using var codec = Open(output);
        Assert.Equal(200, codec.Info.Width);
        Assert.Equal(100, codec.Info.Height);
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void Resize_TransparentPng_BecomesWhiteInJpeg()
    {
        var output = _resizer.Resize(Picture(40, 40, colour: SKColors.Transparent), 40, DerivativeFormat.Jpeg, 90, Cap);

        using var bitmap = SKBitmap.Decode(output);
        var pixel = bitmap.GetPixel(20, 20);
        Assert.True(pixel.Red > 240 && pixel.Green > 240 && pixel.Blue > 240);
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void Probe_ImageAboveThePixelCap_IsRejectedBeforeDecoding()
    {
        var ex = Assert.Throws<ImageRejectedException>(() => _resizer.Probe(Picture(1000, 1000), maxPixels: 500_000));

        Assert.Equal("TOO_LARGE", ex.Code);
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void Resize_ImageAboveThePixelCap_IsRejected()
    {
        var ex = Assert.Throws<ImageRejectedException>(() => _resizer.Resize(Picture(1000, 1000), 320, DerivativeFormat.Webp, 80, maxPixels: 500_000));

        Assert.Equal("TOO_LARGE", ex.Code);
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void Probe_BytesThatAreNotAnImage_AreRejected()
    {
        var ex = Assert.Throws<ImageRejectedException>(() => _resizer.Probe("<svg onload=alert(1)></svg>"u8, Cap));

        Assert.Equal("NOT_AN_IMAGE", ex.Code);
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void Probe_AnImageTypeTheStorefrontDoesNotProcess_IsRejected()
    {
        // a tiny GIF: a real image format, but not one of JPEG, PNG or WebP
        byte[] gif = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0, 0x80, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 0x44, 1, 0, 0x3B];

        var ex = Assert.Throws<ImageRejectedException>(() => _resizer.Probe(gif, Cap));

        Assert.Equal("UNSUPPORTED_TYPE", ex.Code);
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void Resize_OutputOfAnImageWithMetadata_CarriesNoneOfIt()
    {
        var original = Picture(200, 200, SKEncodedImageFormat.Jpeg);
        var marker = Encoding.ASCII.GetBytes("SECRET-GPS-LOCATION");
        // splice an APP1 (EXIF-style) segment holding the marker right after the SOI marker
        byte[] app1 = [0xFF, 0xE1, 0, (byte)(marker.Length + 2), .. marker];
        byte[] withMetadata = [.. original.Take(2), .. app1, .. original.Skip(2)];

        var output = _resizer.Resize(withMetadata, 100, DerivativeFormat.Jpeg, 80, Cap);

        Assert.DoesNotContain("SECRET-GPS-LOCATION", Encoding.Latin1.GetString(output), StringComparison.Ordinal);
    }
}
