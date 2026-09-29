namespace Romp.Modules.Catalog.Domain.Styles;

/// <summary>Maps to CTLG.STYL_CLR_MAP - which colourways a style comes in (composite PK, no surrogate id).</summary>
public sealed class StyleColourway
{
    private StyleColourway()
    {
    }

    internal StyleColourway(long styleId, short colourId)
    {
        StyleId = styleId;
        ColourId = colourId;
    }

    public long StyleId { get; private set; }

    public short ColourId { get; private set; }
}
