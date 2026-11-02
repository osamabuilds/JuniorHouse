namespace Romp.Modules.Cart.Application.Carts;

/// <summary>Why a line needs the shopper's attention. A flagged line is never silently dropped or changed (CRT-7, CRT-8).</summary>
[Flags]
public enum LineIssues
{
    None = 0,

    /// <summary>The price is not what the shopper last saw (CRT-8).</summary>
    PriceChanged = 1,

    /// <summary>Fewer units are available than the quantity in the basket (CRT-7).</summary>
    LowStock = 2,

    /// <summary>The variant can no longer be bought: not published, not labelled, not priced or withdrawn (CRT-5).</summary>
    Unavailable = 4,
}

/// <summary>One basket line as the shopper sees it, priced and checked at the moment of reading (SEC-09).</summary>
public sealed record PricedLine(
    long VariantId,
    long ProductId,
    string Name,
    string Path,
    string Colour,
    string Size,
    string? Sku,
    decimal? UnitPrice,
    decimal? CompareAtPrice,
    int Quantity,
    decimal LineTotal,
    int Available,
    decimal? PreviousPrice,
    LineIssues Issues,
    string? ImageUrl,
    string? ImageFallbackUrl,
    string? ImageAlt)
{
    public bool IsOrderable => Issues is LineIssues.None or LineIssues.PriceChanged;
}

/// <summary>
/// The basket after the server has priced every line and checked every stock level (CRT-1, 7, 8). The subtotal counts only lines that can be
/// bought. Discount, shipping and total are added by the totals feature, which uses the one pricing routine (TOT-*).
/// </summary>
public sealed record PricedCart(IReadOnlyList<PricedLine> Lines, int ItemCount, decimal Subtotal)
{
    public static PricedCart Empty { get; } = new([], 0, 0m);

    public bool IsEmpty => Lines.Count == 0;

    /// <summary>True when any line is unavailable or short of stock: checkout must stop until the shopper fixes the basket.</summary>
    public bool HasProblems => Lines.Any(l => l.Issues.HasFlag(LineIssues.Unavailable) || l.Issues.HasFlag(LineIssues.LowStock));
}
