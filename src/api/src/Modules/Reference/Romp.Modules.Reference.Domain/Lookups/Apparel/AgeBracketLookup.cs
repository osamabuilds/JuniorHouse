namespace Romp.Modules.Reference.Domain.Lookups.Apparel;

/// <summary>Maps to REF.AGE_BRKT_LKP.</summary>
public sealed class AgeBracketLookup : Lookup
{
    private AgeBracketLookup()
    {
    }

    public AgeBracketLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
