namespace Romp.Modules.Reference.Domain.Lookups.Vendors;

/// <summary>
/// Maps to REF.VNDR_COMM_CHNL_LKP. Staff-maintained (WhatsApp, PhoneCall, Email, InPerson,
/// Unspecified - the last one reserved for the legacy/system-recorded acknowledge path and
/// backfill, spec section D).
/// </summary>
public sealed class VendorCommChannelLookup : Lookup
{
    private VendorCommChannelLookup()
    {
    }

    public VendorCommChannelLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
