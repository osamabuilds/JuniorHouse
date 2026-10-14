using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Catalog.Domain.Products;

/// <summary>
/// The read-side copy of one Labelling variant (ADR 0014): its style, colour, size, SKU, GTIN and whether the GTIN was voided. Written
/// only by the Labelling consumer, the refresh and the creation backfill, always by upsert on <see cref="LabellingVariantId"/>, so
/// repeating or reordering events cannot corrupt it (PRD-15). Maps to CTLG.VRNT_LBL.
/// </summary>
public sealed class LabelledVariant : Entity<long>, IAuditable
{
    private LabelledVariant()
    {
        SkuCode = string.Empty;
        Gtin = string.Empty;
    }

    public LabelledVariant(long labellingVariantId, long styleId, short colourId, short sizeId, string skuCode, string gtin, bool gtinVoided)
    {
        LabellingVariantId = labellingVariantId;
        StyleId = styleId;
        ColourId = colourId;
        SizeId = sizeId;
        SkuCode = skuCode;
        Gtin = gtin;
        GtinVoided = gtinVoided;
    }

    public long LabellingVariantId { get; private set; }

    public long StyleId { get; private set; }

    public short ColourId { get; private set; }

    public short SizeId { get; private set; }

    public string SkuCode { get; private set; }

    public string Gtin { get; private set; }

    public bool GtinVoided { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Takes Labelling's current facts; true when anything changed.</summary>
    public bool Refresh(long styleId, short colourId, short sizeId, string skuCode, string gtin, bool gtinVoided)
    {
        if (StyleId == styleId && ColourId == colourId && SizeId == sizeId && SkuCode == skuCode && Gtin == gtin && GtinVoided == gtinVoided)
        {
            return false;
        }

        StyleId = styleId;
        ColourId = colourId;
        SizeId = sizeId;
        SkuCode = skuCode;
        Gtin = gtin;
        GtinVoided = gtinVoided;
        return true;
    }
}
