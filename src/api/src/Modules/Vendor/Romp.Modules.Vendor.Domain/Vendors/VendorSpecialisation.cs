namespace Romp.Modules.Vendor.Domain.Vendors;

/// <summary>Maps to VNDR.VNDR_SPCL_MAP - a vendor can hold more than one specialisation (spec Decisions, AC-5a).</summary>
public sealed class VendorSpecialisation
{
    private VendorSpecialisation()
    {
    }

    internal VendorSpecialisation(long vendorId, short specialisationId)
    {
        VendorId = vendorId;
        SpecialisationId = specialisationId;
    }

    public long VendorId { get; private set; }

    public short SpecialisationId { get; private set; }
}
