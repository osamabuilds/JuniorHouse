namespace Romp.Modules.Reference.Domain;

/// <summary>
/// Maps to REF.PO_CNCL_RSN_LKP. Staff-maintained like the other lookups (tasks.md's scoping note:
/// a cancel reason is exactly the kind of reason code the BRD's own examples say should be
/// addable from the admin panel without a deployment), unlike the system-owned <see cref="PoStatusLookup"/>.
/// </summary>
public sealed class PoCancelReasonLookup : Lookup
{
    private PoCancelReasonLookup()
    {
    }

    public PoCancelReasonLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
