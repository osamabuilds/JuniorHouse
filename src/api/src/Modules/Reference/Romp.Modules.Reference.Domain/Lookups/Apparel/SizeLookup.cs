namespace Romp.Modules.Reference.Domain.Lookups.Apparel;

/// <summary>Maps to REF.SIZE_LKP (e.g. "2-3Y", "4-5Y").</summary>
public sealed class SizeLookup : Lookup
{
    private SizeLookup()
    {
    }

    public SizeLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
