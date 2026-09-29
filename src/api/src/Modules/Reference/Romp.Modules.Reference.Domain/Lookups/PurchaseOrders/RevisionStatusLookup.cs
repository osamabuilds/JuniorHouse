namespace Romp.Modules.Reference.Domain.Lookups.PurchaseOrders;

/// <summary>
/// Maps to REF.PO_REV_STS_LKP. System-owned: seeded by migration only, no staff CRUD, same
/// treatment as <see cref="PoStatusLookup"/> - a PO revision's state machine (Pending, InForce,
/// Superseded, Rejected, Withdrawn, ADR 0007) is fixed application logic, not something staff
/// should be able to add rows to.
/// </summary>
public sealed class RevisionStatusLookup : Lookup
{
    private RevisionStatusLookup()
    {
    }

    public RevisionStatusLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
