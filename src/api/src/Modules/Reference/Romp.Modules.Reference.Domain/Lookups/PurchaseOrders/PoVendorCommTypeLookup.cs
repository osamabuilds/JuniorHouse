namespace Romp.Modules.Reference.Domain.Lookups.PurchaseOrders;

/// <summary>
/// Maps to REF.PO_VNDR_COMM_TYP_LKP. System-owned (Confirmed, Countered, Declined,
/// AmendmentRequest, Decision) - the kind of vendor interaction being recorded is fixed
/// application logic driven by which staff action was used (spec section D).
/// </summary>
public sealed class PoVendorCommTypeLookup : Lookup
{
    private PoVendorCommTypeLookup()
    {
    }

    public PoVendorCommTypeLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
