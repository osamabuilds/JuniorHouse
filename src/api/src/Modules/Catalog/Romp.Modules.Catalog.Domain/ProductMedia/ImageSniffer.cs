namespace Romp.Modules.Catalog.Domain.ProductMedia;

/// <summary>What the bytes of an accepted image say about themselves.</summary>
public sealed record ImageInfo(string ContentType, string Extension, int Width, int Height);

/// <summary>
/// Decides what a file is from its leading bytes and reads its width and height from the header (MED-7, MED-10). Never trusts the file
/// name or the declared content type. Understands only JPEG, PNG and WebP (VP8, VP8L, VP8X); anything else, a truncated header or a
/// zero-size image is refused. No image library: the three headers are small and fixed, so there is no new dependency.
/// </summary>
public static class ImageSniffer
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";

    /// <summary>The largest side accepted, a guard against decompression bombs and typing errors in the header.</summary>
    public const int MaxDimension = 20_000;

    public static ImageInfo? Inspect(ReadOnlySpan<byte> bytes)
    {
        var info = TryPng(bytes) ?? TryJpeg(bytes) ?? TryWebp(bytes);
        return info is { Width: > 0 and <= MaxDimension, Height: > 0 and <= MaxDimension } ? info : null;
    }

    private static ImageInfo? TryPng(ReadOnlySpan<byte> b)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (b.Length < 24 || !b[..8].SequenceEqual(signature) || !b.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        return new ImageInfo(Png, ".png", ReadInt32BigEndian(b, 16), ReadInt32BigEndian(b, 20));
    }

    private static ImageInfo? TryJpeg(ReadOnlySpan<byte> b)
    {
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8 || b[2] != 0xFF)
        {
            return null;
        }

        var i = 2;
        while (i + 3 < b.Length)
        {
            if (b[i] != 0xFF)
            {
                return null;
            }

            var marker = b[i + 1];
            if (marker == 0xFF)
            {
                i++;
                continue;
            }

            // Markers without a length: standalone markers.
            if (marker is 0x01 or (>= 0xD0 and <= 0xD8))
            {
                i += 2;
                continue;
            }

            var length = (b[i + 2] << 8) | b[i + 3];
            if (length < 2)
            {
                return null;
            }

            // Start-of-frame markers (not DHT 0xC4, JPG 0xC8, DAC 0xCC) carry the precision, height and width.
            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
            {
                if (i + 9 > b.Length)
                {
                    return null;
                }

                var height = (b[i + 5] << 8) | b[i + 6];
                var width = (b[i + 7] << 8) | b[i + 8];
                return new ImageInfo(Jpeg, ".jpg", width, height);
            }

            i += 2 + length;
        }

        return null;
    }

    private static ImageInfo? TryWebp(ReadOnlySpan<byte> b)
    {
        if (b.Length < 30 || !b[..4].SequenceEqual("RIFF"u8) || !b.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return null;
        }

        var chunk = b.Slice(12, 4);
        if (chunk.SequenceEqual("VP8 "u8))
        {
            // Lossy: 3-byte frame tag, start code 9D 01 2A, then 14-bit width and height.
            if (b[23] != 0x9D || b[24] != 0x01 || b[25] != 0x2A)
            {
                return null;
            }

            return new ImageInfo(Webp, ".webp", (b[26] | (b[27] << 8)) & 0x3FFF, (b[28] | (b[29] << 8)) & 0x3FFF);
        }

        if (chunk.SequenceEqual("VP8L"u8))
        {
            // Lossless: signature 0x2F, then 14-bit (width - 1) and 14-bit (height - 1).
            if (b[20] != 0x2F)
            {
                return null;
            }

            var bits = (uint)b[21] | ((uint)b[22] << 8) | ((uint)b[23] << 16) | ((uint)b[24] << 24);
            return new ImageInfo(Webp, ".webp", (int)(bits & 0x3FFF) + 1, (int)((bits >> 14) & 0x3FFF) + 1);
        }

        if (chunk.SequenceEqual("VP8X"u8))
        {
            // Extended: 24-bit (canvas width - 1) at offset 24 and (canvas height - 1) at offset 27.
            var width = (b[24] | (b[25] << 8) | (b[26] << 16)) + 1;
            var height = (b[27] | (b[28] << 8) | (b[29] << 16)) + 1;
            return new ImageInfo(Webp, ".webp", width, height);
        }

        return null;
    }

    private static int ReadInt32BigEndian(ReadOnlySpan<byte> b, int offset) =>
        (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
}
