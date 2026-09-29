namespace Romp.Modules.Reference.Domain.Lookups.PurchaseOrders;

/// <summary>
/// Maps to REF.PO_STS_LKP. System-owned: seeded by migration only, no staff CRUD (spec AC-1) -
/// the Create/Update/Retire commands are never registered for this type (see ReferenceModule).
/// </summary>
public sealed class PoStatusLookup : Lookup
{
    private PoStatusLookup()
    {
    }

    public PoStatusLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
