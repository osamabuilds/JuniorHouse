namespace Romp.Modules.Reference.Domain;

/// <summary>
/// Maps to REF.PAYM_TERM_LKP. <see cref="DefaultAdvancePercent"/> is the term's standard advance %
/// (e.g. "50/50" -&gt; 50), copied onto a PO at creation and independently editable per PO (spec
/// Decisions).
/// </summary>
public sealed class PaymentTermLookup : Lookup
{
    private PaymentTermLookup()
    {
    }

    public PaymentTermLookup(string code, string name, string? description, short sortSeq, decimal defaultAdvancePercent)
        : base(code, name, description, sortSeq)
    {
        DefaultAdvancePercent = defaultAdvancePercent;
    }

    public decimal DefaultAdvancePercent { get; private set; }

    public void SetDefaultAdvancePercent(decimal defaultAdvancePercent) => DefaultAdvancePercent = defaultAdvancePercent;
}
