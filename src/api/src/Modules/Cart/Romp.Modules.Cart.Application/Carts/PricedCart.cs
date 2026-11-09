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

    /// <summary>The catalogue category, so a discount code scoped to categories can tell which lines it covers (DSC-7).</summary>
    public short CategoryId { get; init; }

    /// <summary>The id the warehouse knows this variant by (Labelling's variant id), so a stock reservation can name it; null when the variant is not labelled.</summary>
    public long? StockVariantId { get; init; }

    /// <summary>The line carries a struck-through full price, so a code that leaves sale items out skips it (Q-DSC-4).</summary>
    public bool IsOnSale => UnitPrice is { } price && CompareAtPrice is { } was && was > price;
}

/// <summary>
/// The basket after the server has priced every line and checked every stock level (CRT-1, 7, 8). The subtotal counts only lines that can be
/// bought. Discount, shipping and total are added by the totals feature, which uses the one pricing routine (TOT-*).
/// </summary>
public sealed record PricedCart(IReadOnlyList<PricedLine> Lines, int ItemCount, decimal Subtotal)
{
    /// <summary>The basket row this was priced from (null for an empty or missing basket); the order event carries it so the basket is emptied once the order exists (ADR 0025).</summary>
    public long? CartId { get; init; }

    public static PricedCart Empty { get; } = new([], 0, 0m);

    public bool IsEmpty => Lines.Count == 0;

    /// <summary>True when any line is unavailable or short of stock: checkout must stop until the shopper fixes the basket.</summary>
    public bool HasProblems => Lines.Any(l => l.Issues.HasFlag(LineIssues.Unavailable) || l.Issues.HasFlag(LineIssues.LowStock));
}
