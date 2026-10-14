using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Catalog.Domain.CompositionCare;

/// <summary>One fibre and its share of a product's fabric. Maps to CTLG.PRDT_CMPS (unique per product and fibre).</summary>
public sealed class CompositionLine : Entity<long>, IAuditable
{
    private CompositionLine()
    {
    }

    public CompositionLine(long productId, short fibreId, decimal percent)
    {
        ProductId = productId;
        FibreId = fibreId;
        Percent = percent;
    }

    public long ProductId { get; private set; }

    public short FibreId { get; private set; }

    /// <summary>The share in percent with at most one decimal (CMP-7); 0 &lt; p ≤ 100.</summary>
    public decimal Percent { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>A care instruction chosen for a product. Maps to CTLG.PRDT_CARE_MAP (unique per product and instruction).</summary>
public sealed class ProductCare : Entity<long>, IAuditable
{
    private ProductCare()
    {
    }

    public ProductCare(long productId, short careId)
    {
        ProductId = productId;
        CareId = careId;
    }

    public long ProductId { get; private set; }

    public short CareId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// The rules for a product's fabric composition (CMP-1, CMP-6, CMP-7). A composition may be saved incomplete while a product is a
/// draft, but it is complete only at exactly 100.0%. Shares are compared in integer tenths, so 33.3 + 33.3 + 33.4 is exactly 100.
/// </summary>
public static class Composition
{
    public const decimal CompleteTotal = 100m;

    /// <summary>The sum of the shares, exact (decimal arithmetic on tenths).</summary>
    public static decimal Total(IEnumerable<decimal> percents) => percents.Sum(p => decimal.Round(p * 10m)) / 10m;

    public static bool IsComplete(IEnumerable<decimal> percents) => Total(percents) == CompleteTotal;

    /// <summary>The first problem with a set of rows (fibre id, share), or null when they can be saved.</summary>
    public static string? Check(IReadOnlyCollection<(short FibreId, decimal Percent)> rows)
    {
        if (rows.Select(r => r.FibreId).Distinct().Count() != rows.Count)
        {
            return "Each fibre can be listed only once.";
        }

        foreach (var (_, percent) in rows)
        {
            if (percent <= 0 || percent > 100)
            {
                return "Each percentage must be more than 0 and at most 100.";
            }

            if (decimal.Round(percent, 1) != percent)
            {
                return "A percentage can have at most one decimal place.";
            }
        }

        return null;
    }
}
