namespace Romp.Modules.Vendor.Domain;

/// <summary>
/// The REF.PO_STS_LKP row ids this sprint's state machine references (Draft -&gt; SentToVendor -&gt;
/// Acknowledged, or Cancelled from any of those three). PO_STS_LKP is system-owned - staff can't
/// add/edit/reorder its rows (spec AC-1) - and its seed order is fixed in
/// Romp.Modules.Reference.Infrastructure/ReferenceSeedData.cs, so these ids are stable constants
/// rather than something VNDR looks up at runtime. VNDR can't query REF's table directly either
/// way (ADR 0002 - no cross-schema query, and REF has no Contracts project this sprint since
/// nothing else needs to read its lookups programmatically). If PO_STS_LKP's seed order ever
/// changes, update both files together.
/// </summary>
internal static class PoStatus
{
    public const short Draft = 1;
    public const short SentToVendor = 2;
    public const short Acknowledged = 3;
    public const short Cancelled = 4;
}
