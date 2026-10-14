using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Catalog.Domain.Products;

/// <summary>
/// One planned size × colour of a product and its price (PRD-1, PRD-2, PRD-8). Maps to CTLG.PRDT_VRNT. The SKU and GTIN are not here:
/// they belong to Labelling and arrive as <see cref="LabelledVariant"/> (owner decision D1, ADR 0014).
/// </summary>
public sealed class PlannedVariant : Entity<long>, IAuditable
{
    /// <summary>The largest price accepted; anything above is refused as a typing slip (PRD-8).</summary>
    public const decimal MaxPrice = 9_999_999.99m;

    private PlannedVariant()
    {
    }

    internal PlannedVariant(short colourId, short sizeId, decimal? price)
    {
        ColourId = colourId;
        SizeId = sizeId;
        if (price is > 0 and <= MaxPrice)
        {
            Price = Round(price.Value);
        }
    }

    public long ProductId { get; private set; }

    public short ColourId { get; private set; }

    public short SizeId { get; private set; }

    /// <summary>The selling price in PKR; null until priced. Never zero or negative (PRD-8).</summary>
    public decimal? Price { get; private set; }

    /// <summary>The optional "was" price, always higher than <see cref="Price"/> (PRD-2, owner decision D4).</summary>
    public decimal? CompareAtPrice { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>The first problem with a price pair, or null when it is acceptable. One source for the validator, the entity and the database check.</summary>
    public static string? CheckPrices(decimal price, decimal? compareAtPrice)
    {
        if (price <= 0 || price > MaxPrice)
        {
            return $"The price must be more than 0 and at most {MaxPrice:N2}.";
        }

        if (compareAtPrice is { } compare && (compare <= price || compare > MaxPrice))
        {
            return "The compare-at price must be higher than the price.";
        }

        return null;
    }

    /// <summary>Sets the price pair (PRD-2, PRD-8) and returns the history row to record (PRD-7), or null when nothing changed.</summary>
    public PriceHistoryEntry? SetPrice(decimal price, decimal? compareAtPrice, string person)
    {
        var problem = CheckPrices(price, compareAtPrice);
        if (problem is not null)
        {
            throw new DomainException(problem);
        }

        var newPrice = Round(price);
        var newCompare = compareAtPrice is null ? (decimal?)null : Round(compareAtPrice.Value);
        if (Price == newPrice && CompareAtPrice == newCompare)
        {
            return null;
        }

        var entry = new PriceHistoryEntry(this, Price, newPrice, CompareAtPrice, newCompare, person);
        Price = newPrice;
        CompareAtPrice = newCompare;
        return entry;
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// One change to a variant's price (PRD-7): old and new price, compare-at, who and when (the insert audit columns). Append-only: a
/// database trigger refuses an update or delete. Maps to CTLG.PRDT_PRIC_HIST.
/// </summary>
public sealed class PriceHistoryEntry : Entity<long>, IAuditable
{
    private PriceHistoryEntry()
    {
        ChangedBy = string.Empty;
    }

    internal PriceHistoryEntry(PlannedVariant variant, decimal? oldPrice, decimal newPrice, decimal? oldCompareAt, decimal? newCompareAt, string person)
    {
        Variant = variant;
        OldPrice = oldPrice;
        NewPrice = newPrice;
        OldCompareAtPrice = oldCompareAt;
        NewCompareAtPrice = newCompareAt;
        ChangedBy = person;
    }

    public long VariantId { get; private set; }

    public PlannedVariant? Variant { get; private set; }

    public decimal? OldPrice { get; private set; }

    public decimal NewPrice { get; private set; }

    public decimal? OldCompareAtPrice { get; private set; }

    public decimal? NewCompareAtPrice { get; private set; }

    /// <summary>Who made the change, as typed by the acting person (until authentication, S8).</summary>
    public string ChangedBy { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
