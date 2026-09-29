using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain.Revisions;

/// <summary>Maps to VNDR.PO_REV_LINE - one revision's line-quantity snapshot (one row per size x colour), same shape as Sprint 1's <see cref="PoLine"/> but versioned per revision instead of mutable-in-place.</summary>
public sealed class PoRevisionLine : Entity<long>, IAuditable
{
    private PoRevisionLine()
    {
    }

    internal PoRevisionLine(long revisionId, short sizeId, short colourId, int qty)
    {
        RevisionId = revisionId;
        SizeId = sizeId;
        ColourId = colourId;
        Qty = qty;
    }

    public long RevisionId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int Qty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
