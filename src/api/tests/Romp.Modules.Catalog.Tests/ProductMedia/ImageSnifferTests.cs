using System.Text;
using Romp.Modules.Catalog.Domain.ProductMedia;
using Romp.Modules.Catalog.Tests.Support;

namespace Romp.Modules.Catalog.Tests.ProductMedia;

public sealed class ImageSnifferTests
{
    [Fact]
    [Trait("Spec", "MED-7")]
    [Trait("Spec", "MED-10")]
    public void APng_GivesItsTypeAndDimensions()
    {
        var info = ImageSniffer.Inspect(TestImages.Png(640, 480));

        Assert.NotNull(info);
        Assert.Equal(ImageSniffer.Png, info.ContentType);
        Assert.Equal(".png", info.Extension);
        Assert.Equal((640, 480), (info.Width, info.Height));
    }

    [Fact]
    [Trait("Spec", "MED-10")]
    public void AJpeg_GivesItsTypeAndDimensions_FromTheStartOfFrameMarker()
    {
        var info = ImageSniffer.Inspect(TestImages.Jpeg(1200, 800));

        Assert.NotNull(info);
        Assert.Equal(ImageSniffer.Jpeg, info.ContentType);
        Assert.Equal((1200, 800), (info.Width, info.Height));
    }

    [Fact]
    [Trait("Spec", "MED-10")]
    public void ALosslessWebp_GivesItsTypeAndDimensions()
    {
        var info = ImageSniffer.Inspect(TestImages.WebpLossless(300, 450));

        Assert.NotNull(info);
        Assert.Equal(ImageSniffer.Webp, info.ContentType);
        Assert.Equal((300, 450), (info.Width, info.Height));
    }

    [Fact]
    [Trait("Spec", "MED-10")]
    public void ALossyWebp_GivesItsDimensions()
    {
        byte[] file =
        [
            .. "RIFF"u8.ToArray(), 30, 0, 0, 0, .. "WEBP"u8.ToArray(), .. "VP8 "u8.ToArray(), 18, 0, 0, 0,
            0x10, 0x02, 0x00, 0x9D, 0x01, 0x2A, 0x20, 0x03, 0x58, 0x02, 0, 0, 0, 0, 0, 0,
        ];

        var info = ImageSniffer.Inspect(file);

        Assert.NotNull(info);
        Assert.Equal((800, 600), (info.Width, info.Height));
    }

    [Fact]
    [Trait("Spec", "MED-10")]
    public void AnExtendedWebp_GivesTheCanvasDimensions()
    {
        byte[] file =
        [
            .. "RIFF"u8.ToArray(), 30, 0, 0, 0, .. "WEBP"u8.ToArray(), .. "VP8X"u8.ToArray(), 10, 0, 0, 0,
            0, 0, 0, 0, 0x7F, 0x02, 0x00, 0xDF, 0x01, 0x00, 0, 0, 0, 0,
        ];

        var info = ImageSniffer.Inspect(file);

        Assert.NotNull(info);
        Assert.Equal((640, 480), (info.Width, info.Height));
    }

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("MZ\u0090\u0000\u0003 this is an exe")]
    [InlineData("GIF89a\u0001\u0000\u0001\u0000")]
    [InlineData("%PDF-1.7 fake.jpg")]
    [InlineData("")]
    [Trait("Spec", "MED-7")]
    public void AFileThatIsNotAnAcceptedImage_IsRefused_WhateverItIsCalled(string content) =>
        Assert.Null(ImageSniffer.Inspect(Encoding.Latin1.GetBytes(content)));

    [Fact]
    [Trait("Spec", "MED-7")]
    public void ATruncatedHeader_IsRefused()
    {
        var png = TestImages.Png(10, 10);

        Assert.Null(ImageSniffer.Inspect(png.AsSpan(0, 16)));
        Assert.Null(ImageSniffer.Inspect(TestImages.Jpeg(10, 10).AsSpan(0, 12)));
        Assert.Null(ImageSniffer.Inspect(TestImages.WebpLossless(10, 10).AsSpan(0, 20)));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(20_001, 10)]
    [Trait("Spec", "MED-7")]
    public void AZeroOrAbsurdSize_IsRefused(int width, int height) => Assert.Null(ImageSniffer.Inspect(TestImages.Png(width, height)));

    [Fact]
    [Trait("Spec", "MED-7")]
    public void APolyglot_IsTreatedByItsLeadingBytes_AndStillOnlyServedWithOurFixedType()
    {
        var polyglot = TestImages.Png(5, 5).Concat(Encoding.ASCII.GetBytes("<script>alert(1)</script>")).ToArray();

        var info = ImageSniffer.Inspect(polyglot);

        Assert.NotNull(info);
        Assert.Equal(ImageSniffer.Png, info.ContentType);
    }
}
