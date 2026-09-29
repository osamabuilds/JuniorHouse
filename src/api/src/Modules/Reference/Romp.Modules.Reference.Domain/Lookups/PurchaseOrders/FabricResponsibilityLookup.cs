namespace Romp.Modules.Reference.Domain.Lookups.PurchaseOrders;

/// <summary>
/// Maps to REF.FBRC_RESP_LKP. System-owned (VendorSupplied, RompSupplied) - decides which
/// production milestones exist once Sprint 3 builds Production Tracking (plan.md's Problem &amp;
/// intent), so it's fixed application logic, not a staff-editable classification, same treatment
/// as <see cref="PoStatusLookup"/>.
/// </summary>
public sealed class FabricResponsibilityLookup : Lookup
{
    private FabricResponsibilityLookup()
    {
    }

    public FabricResponsibilityLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
