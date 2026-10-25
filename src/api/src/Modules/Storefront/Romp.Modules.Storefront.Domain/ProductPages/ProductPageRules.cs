using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Domain.ProductPages;

/// <summary>A product path that passed the checks, and whether normalising changed it (a change is answered with a 301, URL-5).</summary>
public sealed record NormalisedPath(string Value, bool Changed);

public static class ProductPath
{
    public const int MaxLength = 200;

    /// <summary>
    /// Lower-cases the path, adds the leading slash and drops trailing slashes. Null for anything that cannot be a product path: empty,
    /// longer than <see cref="MaxLength"/>, a query or fragment, a backslash, a dot segment, an empty segment or a control character. The path is
    /// only ever a lookup key; it never reaches the file system.
    /// </summary>
    public static NormalisedPath? Normalise(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > MaxLength)
        {
            return null;
        }

        if (raw.Any(c => char.IsControl(c) || c is '?' or '#' or '\\' or ' '))
        {
            return null;
        }

        var trimmed = raw.TrimEnd('/');
        var value = (trimmed.StartsWith('/') ? trimmed : "/" + trimmed).ToLowerInvariant();
        var segments = value[1..].Split('/');
        if (segments.Any(s => s.Length == 0 || s is "." or ".."))
        {
            return null;
        }

        return new NormalisedPath(value, !string.Equals(value, raw, StringComparison.Ordinal));
    }
}

/// <summary>One variant as the selection and price rules see it.</summary>
public sealed record VariantFact(
    short ColourId, string ColourCode, string SizeCode, AvailabilityClass Availability, decimal? Price, decimal? CompareAtPrice);

/// <summary>The colour and size the page shows. <c>SizeCode</c> is null until a size of that colour is chosen.</summary>
public sealed record PageSelection(short ColourId, string ColourCode, string? SizeCode);

public static class ProductSelection
{
    /// <summary>
    /// The default colour is the first with a size that can be bought, else the first colour. A colour or size in the query that the product
    /// does not have is ignored (PDP-2, 3); a size only counts within the chosen colour. Variants arrive in display order. Null when the
    /// product has no variants.
    /// </summary>
    public static PageSelection? Resolve(IReadOnlyList<VariantFact> variants, string? colour, string? size)
    {
        ArgumentNullException.ThrowIfNull(variants);
        if (variants.Count == 0)
        {
            return null;
        }

        var chosen = colour is null ? null : variants.FirstOrDefault(v => Same(v.ColourCode, colour));
        chosen ??= variants.FirstOrDefault(v => v.Availability.IsPurchasable()) ?? variants[0];

        var sized = size is null
            ? null
            : variants.FirstOrDefault(v => v.ColourId == chosen.ColourId && Same(v.SizeCode, size));
        return new PageSelection(chosen.ColourId, chosen.ColourCode, sized?.SizeCode);
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>The price line: the amount, the struck-through compare-at when it is above it, and whether it is a "From" price (PDP-9, 15).</summary>
public sealed record DisplayedPrice(decimal Price, decimal? CompareAtPrice, bool IsFrom);

public static class ProductPricing
{
    /// <summary>
    /// With a size chosen, that variant's price. Before a choice, the lowest price among the chosen colour's variants that can be bought
    /// (all priced ones when none can), shown as "From" only when the prices differ. A compare-at price is passed on only when it is above
    /// the selling price, so a page never invents a "was" price. Null when nothing is priced.
    /// </summary>
    public static DisplayedPrice? For(IReadOnlyList<VariantFact> variants, PageSelection selection)
    {
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(selection);

        var ofColour = variants.Where(v => v.ColourId == selection.ColourId && v.Price is not null).ToList();
        if (selection.SizeCode is not null)
        {
            var one = ofColour.FirstOrDefault(v => string.Equals(v.SizeCode, selection.SizeCode, StringComparison.OrdinalIgnoreCase));
            if (one is not null)
            {
                return new DisplayedPrice(one.Price!.Value, Struck(one), false);
            }
        }

        var counted = ofColour.Where(v => v.Availability.IsPurchasable()).ToList();
        if (counted.Count == 0)
        {
            counted = ofColour;
        }

        if (counted.Count == 0)
        {
            return null;
        }

        var lowest = counted.MinBy(v => v.Price!.Value)!;
        var differs = counted.Any(v => v.Price != lowest.Price);
        return new DisplayedPrice(lowest.Price!.Value, differs ? null : Struck(lowest), differs);
    }

    private static decimal? Struck(VariantFact v) => v.CompareAtPrice is { } c && c > v.Price ? c : null;
}

/// <summary>An image's place in the gallery. <c>ColourId</c> null means a product-level image.</summary>
public sealed record ImageFact(long Id, short? ColourId, int SortSeq);

public static class GalleryImages
{
    /// <summary>
    /// The images shown for a colour, in sort order: that colour's own images when it has any, otherwise the product-level ones (PDP-2).
    /// Images of other colours are never shown.
    /// </summary>
    public static IReadOnlyList<ImageFact> For(IEnumerable<ImageFact> images, short colourId)
    {
        ArgumentNullException.ThrowIfNull(images);
        var all = images.ToList();
        var own = all.Where(i => i.ColourId == colourId).ToList();
        var chosen = own.Count > 0 ? own : all.Where(i => i.ColourId is null).ToList();
        return chosen.OrderBy(i => i.SortSeq).ThenBy(i => i.Id).ToList();
    }
}
