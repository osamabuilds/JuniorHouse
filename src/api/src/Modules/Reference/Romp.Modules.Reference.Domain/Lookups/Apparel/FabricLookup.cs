namespace Romp.Modules.Reference.Domain.Lookups.Apparel;

/// <summary>Maps to REF.FBRC_LKP.</summary>
public sealed class FabricLookup : Lookup
{
    private FabricLookup()
    {
    }

    public FabricLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
