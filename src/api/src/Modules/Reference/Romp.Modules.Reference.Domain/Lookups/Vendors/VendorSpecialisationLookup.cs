namespace Romp.Modules.Reference.Domain.Lookups.Vendors;

/// <summary>Maps to REF.VNDR_SPCL_LKP (e.g. knits, wovens, uniforms).</summary>
public sealed class VendorSpecialisationLookup : Lookup
{
    private VendorSpecialisationLookup()
    {
    }

    public VendorSpecialisationLookup(string code, string name, string? description, short sortSeq)
        : base(code, name, description, sortSeq)
    {
    }
}
