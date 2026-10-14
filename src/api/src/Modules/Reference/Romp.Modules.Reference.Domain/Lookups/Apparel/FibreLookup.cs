namespace Romp.Modules.Reference.Domain.Lookups.Apparel;

/// <summary>Maps to REF.FIBR_LKP (Sprint 7, Catalog): the fibres a garment's composition is made of.</summary>
public sealed class FibreLookup : Lookup
{
    private FibreLookup()
    {
    }

    public FibreLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
