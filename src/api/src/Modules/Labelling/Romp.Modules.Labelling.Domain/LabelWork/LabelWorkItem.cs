using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Labelling.Domain.LabelWork;

/// <summary>
/// One accepted line of a QC sub-batch waiting for tags (LW-1). Unique per <c>(SubBatchId, SizeId, ColourId)</c>, so a
/// redelivered QC message cannot create it twice. The labelled quantity is derived from printed jobs, never stored here.
/// Maps to SKU.LBL_WORK_ITEM.
/// </summary>
public sealed class LabelWorkItem : Entity<long>, IAuditable
{
    private LabelWorkItem()
    {
    }

    public LabelWorkItem(long subBatchId, long qcBatchId, long poId, short sizeId, short colourId, int qty, short outcomeId)
    {
        if (qty <= 0)
        {
            throw new DomainException("A work item needs a positive quantity.");
        }

        SubBatchId = subBatchId;
        QcBatchId = qcBatchId;
        PoId = poId;
        SizeId = sizeId;
        ColourId = colourId;
        Qty = qty;
        OutcomeId = outcomeId;
        StatusId = LabelWorkStatus.NeedsAttention;
        ParkReasonId = ParkReasons.NoStyle;
    }

    public long SubBatchId { get; private set; }

    public long QcBatchId { get; private set; }

    public long PoId { get; private set; }

    public long? StyleId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int Qty { get; private set; }

    public short OutcomeId { get; private set; }

    public short StatusId { get; private set; }

    public short? ParkReasonId { get; private set; }

    public long? VariantId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Cannot be processed yet (LW-4): no style for the PO, or the style is retired and staff must confirm.</summary>
    public void Park(short reasonId, long? styleId)
    {
        StyleId = styleId;
        StatusId = LabelWorkStatus.NeedsAttention;
        ParkReasonId = reasonId;
        VariantId = null;
    }

    /// <summary>The variant has its GTIN; the item is ready to print.</summary>
    public void MarkReady(long styleId, long variantId)
    {
        StyleId = styleId;
        VariantId = variantId;
        StatusId = LabelWorkStatus.Ready;
        ParkReasonId = null;
    }
}
