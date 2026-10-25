using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Romp.Modules.Storefront.Domain.PublishedCatalog;

/// <summary>The formats a resized image is written in (P-5): WebP, with a JPEG fallback.</summary>
public enum DerivativeFormat : short
{
    Webp = 1,
    Jpeg = 2,
}

public static class DerivativeFormats
{
    /// <summary>The code stored in <c>IMG_DRV.FMT</c> (<c>WEBP</c> or <c>JPG</c>).</summary>
    public static string Code(this DerivativeFormat format) => format switch
    {
        DerivativeFormat.Webp => "WEBP",
        DerivativeFormat.Jpeg => "JPG",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    /// <summary>The file extension, without the dot.</summary>
    public static string Extension(this DerivativeFormat format) => format switch
    {
        DerivativeFormat.Webp => "webp",
        DerivativeFormat.Jpeg => "jpg",
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };
}

/// <summary>
/// The file name of a resized image: <c>{contentHash}-{width}.{webp|jpg}</c> (PCT-7, PCT-10). Names are generated here from a hash of the
/// original's bytes, a width and a format, and are never built from client input, so a path can never escape the media folder. A
/// changed image has a new hash and so new names; the old files are removed after the cache lifetime. The pattern is strict
/// so a request path that is not a generated name is refused before it reaches storage.
/// </summary>
public static class DerivativeName
{
    public const int MinWidth = 1;
    public const int MaxWidth = 8192;

    private static readonly Regex HashPattern = new("^[0-9a-f]{32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NamePattern = new("^(?<hash>[0-9a-f]{32})-(?<width>[0-9]{1,4})\\.(?<ext>webp|jpg)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The first 128 bits of the SHA-256 of the original image, as 32 lowercase hex characters.</summary>
    public static string HashOf(ReadOnlySpan<byte> original) =>
        Convert.ToHexStringLower(SHA256.HashData(original)).Substring(0, 32);

    public static string For(string hash, int width, DerivativeFormat format)
    {
        if (hash is null || !HashPattern.IsMatch(hash))
        {
            throw new ArgumentException("A content hash is 32 lowercase hex characters.", nameof(hash));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(width, MinWidth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaxWidth);
        return string.Create(CultureInfo.InvariantCulture, $"{hash}-{width}.{format.Extension()}");
    }

    /// <summary>Reads a generated name back; false for anything else (wrong case, extra path, unknown extension, width out of range).</summary>
    public static bool TryParse(string? candidate, out string hash, out int width, out DerivativeFormat format)
    {
        hash = string.Empty;
        width = 0;
        format = default;
        if (candidate is null)
        {
            return false;
        }

        var match = NamePattern.Match(candidate);
        if (!match.Success
            || !int.TryParse(match.Groups["width"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedWidth)
            || parsedWidth is < MinWidth or > MaxWidth)
        {
            return false;
        }

        hash = match.Groups["hash"].Value;
        width = parsedWidth;
        format = match.Groups["ext"].Value == "webp" ? DerivativeFormat.Webp : DerivativeFormat.Jpeg;
        return true;
    }
}
