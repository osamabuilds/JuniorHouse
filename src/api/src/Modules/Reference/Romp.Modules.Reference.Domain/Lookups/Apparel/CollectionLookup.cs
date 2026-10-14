namespace Romp.Modules.Reference.Domain.Lookups.Apparel;

/// <summary>Maps to REF.COLN_LKP (Sprint 7, Catalog): merchandising collections such as New Arrivals or Sale.</summary>
public sealed class CollectionLookup : Lookup
{
    private CollectionLookup()
    {
    }

    public CollectionLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
