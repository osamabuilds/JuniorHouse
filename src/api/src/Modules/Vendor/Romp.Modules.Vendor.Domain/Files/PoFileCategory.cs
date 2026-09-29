using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Domain.Files;

/// <summary>
/// Ids of REF.PO_FILE_CATG_LKP's seeded, system-owned rows (spec section F). The vendor-visible /
/// internal split is structural, not staff-editable, so code may depend on it (same reasoning as
/// <see cref="PoStatus"/>).
/// </summary>
public static class PoFileCategory
{
    public const short TechPackSpec = 1;
    public const short VendorEvidence = 8;
    private const short LastVendorVisible = 5;
    private const short LastKnown = 9;

    public static bool IsKnown(short categoryId) => categoryId is >= 1 and <= LastKnown;

    /// <summary>Tech pack, artwork/labels, trim card/BOM, colour standard, packing instructions.</summary>
    public static bool IsVendorVisible(short categoryId) => categoryId is >= 1 and <= LastVendorVisible;
}
