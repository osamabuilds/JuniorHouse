namespace Romp.Modules.Reference.Domain.Lookups.Apparel;

/// <summary>Maps to REF.GNDR_LKP.</summary>
public sealed class GenderLookup : Lookup
{
    private GenderLookup()
    {
    }

    public GenderLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
