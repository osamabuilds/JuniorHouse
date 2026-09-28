namespace Romp.Modules.Reference.Domain;

/// <summary>
/// Maps to REF.AMND_INIT_LKP. System-owned (Buyer, Vendor) - who proposed a PO revision is fixed
/// application logic, not a staff-editable classification.
/// </summary>
public sealed class AmendmentInitiatorLookup : Lookup
{
    private AmendmentInitiatorLookup()
    {
    }

    public AmendmentInitiatorLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
