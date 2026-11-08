namespace Romp.Modules.Storefront.Domain.ProductRecommendations;

/// <summary>What the field score looks at for one product: category, colours and lowest price.</summary>
public sealed record SimilarityProfile(string CategoryCode, IReadOnlyCollection<string> ColourCodes, decimal? LowestPrice);

/// <summary>How alike two products are by their catalogue fields (REC-2): 0.5 category, 0.3 colour overlap, 0.2 price closeness.</summary>
public static class FieldSimilarity
{
    public const double CategoryWeight = 0.5;
    public const double ColourWeight = 0.3;
    public const double PriceWeight = 0.2;

    public static double Score(SimilarityProfile a, SimilarityProfile b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var category = string.Equals(a.CategoryCode, b.CategoryCode, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        return (CategoryWeight * category) + (ColourWeight * ColourOverlap(a.ColourCodes, b.ColourCodes)) + (PriceWeight * PriceCloseness(a.LowestPrice, b.LowestPrice));
    }

    /// <summary>Jaccard overlap of the colour codes: shared colours over all colours; 0 when either has none.</summary>
    public static double ColourOverlap(IReadOnlyCollection<string> a, IReadOnlyCollection<string> b)
    {
        var left = new HashSet<string>(a, StringComparer.OrdinalIgnoreCase);
        var right = new HashSet<string>(b, StringComparer.OrdinalIgnoreCase);
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var shared = left.Count(right.Contains);
        return (double)shared / (left.Count + right.Count - shared);
    }

    /// <summary>1 for equal prices, falling to 0 at a tenfold gap (<c>1 - min(1, |ln(pa/pb)| / ln 10)</c>); 0 when a price is unknown.</summary>
    public static double PriceCloseness(decimal? a, decimal? b)
    {
        if (a is not > 0 || b is not > 0)
        {
            return 0;
        }

        var gap = Math.Abs(Math.Log((double)a.Value / (double)b.Value)) / Math.Log(10);
        return 1 - Math.Min(1, gap);
    }
}
