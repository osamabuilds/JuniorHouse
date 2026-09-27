namespace Romp.Modules.Catalog.Domain;

/// <summary>Maps to CTLG.STYL_SIZE_MAP - a style's size run (composite PK, no surrogate id).</summary>
public sealed class StyleSize
{
    private StyleSize()
    {
    }

    internal StyleSize(long styleId, short sizeId)
    {
        StyleId = styleId;
        SizeId = sizeId;
    }

    public long StyleId { get; private set; }

    public short SizeId { get; private set; }
}
