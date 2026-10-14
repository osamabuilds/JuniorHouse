namespace Romp.Modules.Labelling.Contracts;

/// <summary>
/// A variant as another module may see it (ADR 0014): the colour × size of a style, its SKU, and the GTIN it holds now. When the
/// variant's GTIN has been voided and not replaced, <see cref="Gtin"/> is the last one it held and <see cref="GtinVoided"/> is true.
/// </summary>
public sealed record LabelledVariantInfo(long VariantId, long StyleId, short ColourId, short SizeId, string SkuCode, string Gtin, bool GtinVoided);

/// <summary>
/// The read side of the Labelling contract for Catalog (Sprint 7, ADR 0014). One query however many variants a style has; a variant
/// that never received a GTIN is absent. Labelling's tables are never read by another module (ADR 0002).
/// </summary>
public interface IVariantQueries
{
    Task<IReadOnlyList<LabelledVariantInfo>> GetByStyleAsync(long styleId, CancellationToken cancellationToken);

    Task<LabelledVariantInfo?> GetByIdAsync(long variantId, CancellationToken cancellationToken);
}
