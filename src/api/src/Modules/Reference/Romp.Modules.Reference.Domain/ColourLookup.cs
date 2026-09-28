namespace Romp.Modules.Reference.Domain;

/// <summary>Maps to REF.CLR_LKP.</summary>
public sealed class ColourLookup : Lookup
{
    private ColourLookup()
    {
    }

    public ColourLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
