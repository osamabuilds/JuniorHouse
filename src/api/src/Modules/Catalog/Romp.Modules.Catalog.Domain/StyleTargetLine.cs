using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Catalog.Domain;

/// <summary>
/// Maps to CTLG.STYL_TGT_LINE - target quantity for one size x colour cell. Unlike
/// <see cref="StyleColourway"/>/<see cref="StyleSize"/>, this is a real transactional row (its own
/// surrogate id, audit columns) because it carries a value (target quantity), not just a
/// membership fact.
/// </summary>
public sealed class StyleTargetLine : Entity<long>, IAuditable
{
    private StyleTargetLine()
    {
    }

    internal StyleTargetLine(long styleId, short sizeId, short colourId, int targetQty)
    {
        StyleId = styleId;
        SizeId = sizeId;
        ColourId = colourId;
        TargetQty = targetQty;
    }

    public long StyleId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int TargetQty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
