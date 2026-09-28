namespace Romp.Modules.Reference.Domain;

/// <summary>
/// Maps to REF.PO_FILE_CATG_LKP. System-owned - <see cref="IsVendorVisible"/> is structural (which
/// categories are spec files that become part of a revision vs. internal files Romp never shows
/// the vendor, ADR 0007/spec section F), so staff cannot edit the split (plan.md's Risks section:
/// getting this wrong is a data-leak risk, not a flexibility win).
/// </summary>
public sealed class PoFileCategoryLookup : Lookup
{
    private PoFileCategoryLookup()
    {
    }

    public PoFileCategoryLookup(string code, string name, string? description, short sortSeq, bool isVendorVisible)
        : base(code, name, description, sortSeq)
    {
        IsVendorVisible = isVendorVisible;
    }

    public bool IsVendorVisible { get; private set; }
}
