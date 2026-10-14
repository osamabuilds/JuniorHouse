namespace Romp.Modules.Catalog.Domain.Products;

/// <summary>Why a planned variant can or cannot be bought (PRD-1, PRD-13, PRD-14).</summary>
public enum VariantAvailability
{
    /// <summary>Labelling has not issued a GTIN yet: shown as "not yet available".</summary>
    NotYetAvailable = 1,

    /// <summary>Labelling issued a GTIN, but it was voided and not replaced: not sellable.</summary>
    GtinVoided = 2,

    /// <summary>A GTIN is issued but the variant has no price yet: "not yet available" until priced (Q-PRD-8).</summary>
    Unpriced = 3,

    /// <summary>A GTIN is issued, not voided, and a price is set: can be bought.</summary>
    Sellable = 4,
}

/// <summary>One planned variant joined with Labelling's facts for the product page and the editor.</summary>
public sealed record VariantView(
    long VariantId,
    short ColourId,
    short SizeId,
    decimal? Price,
    decimal? CompareAtPrice,
    string? SkuCode,
    string? Gtin,
    VariantAvailability Availability)
{
    public bool IsSellable => Availability == VariantAvailability.Sellable;
}

/// <summary>
/// The pure join of a product's planned variants with Labelling's variants by colour and size (ADR 0014). Sellability is derived every
/// time and never stored, so it cannot drift when Labelling voids or re-issues a GTIN.
/// </summary>
public static class SellableVariants
{
    public static IReadOnlyList<VariantView> Join(IEnumerable<PlannedVariant> planned, IEnumerable<LabelledVariant> labelled)
    {
        // Several Labelling variants can exist for one colour × size (a voided GTIN, later replaced): the newest live one wins,
        // else the newest voided one.
        var byKey = labelled
            .GroupBy(l => (l.ColourId, l.SizeId))
            .ToDictionary(
                g => g.Key,
                g => g.Where(l => !l.GtinVoided).OrderByDescending(l => l.LabellingVariantId).FirstOrDefault()
                    ?? g.OrderByDescending(l => l.LabellingVariantId).First());

        return planned
            .OrderBy(p => p.ColourId).ThenBy(p => p.SizeId)
            .Select(p =>
            {
                byKey.TryGetValue((p.ColourId, p.SizeId), out var label);
                var availability = label is null ? VariantAvailability.NotYetAvailable
                    : label.GtinVoided ? VariantAvailability.GtinVoided
                    : p.Price is > 0 ? VariantAvailability.Sellable
                    : VariantAvailability.Unpriced;
                return new VariantView(p.Id, p.ColourId, p.SizeId, p.Price, p.CompareAtPrice, label?.SkuCode, label?.Gtin, availability);
            })
            .ToList();
    }
}
