namespace Romp.Modules.Reference.Domain.Lookups.PurchaseOrders;

/// <summary>
/// Maps to REF.AMND_RSN_LKP. Staff-maintained like most Sprint 1 lookups - an amendment reason is
/// exactly the kind of reason code that should be addable from the admin panel without a
/// deployment (spec SCRUM-93, plan.md's Non-functional constraints).
/// </summary>
public sealed class AmendmentReasonLookup : Lookup
{
    private AmendmentReasonLookup()
    {
    }

    public AmendmentReasonLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
