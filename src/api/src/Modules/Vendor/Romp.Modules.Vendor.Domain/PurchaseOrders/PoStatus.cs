
namespace Romp.Modules.Vendor.Domain.PurchaseOrders;

/// <summary>
/// The REF.PO_STS_LKP row ids the PO state machine references (BRD §5.9): Draft -&gt; SentToVendor -&gt;
/// Acknowledged -&gt; InProduction -&gt; PartiallyDelivered -&gt; Delivered -&gt; Closed, plus Cancelled.
/// PO_STS_LKP is system-owned - staff can't add/edit/reorder its rows (Sprint 2 spec AC-1) - and its
/// seed order is fixed in Romp.Modules.Reference.Infrastructure/ReferenceSeedData.cs, so these ids
/// are stable constants (VNDR can't query REF's table, ADR 0002). If the seed changes, update both together.
/// </summary>
internal static class PoStatus
{
    public const short Draft = 1;
    public const short SentToVendor = 2;
    public const short Acknowledged = 3;
    public const short Cancelled = 4;
    public const short InProduction = 5;
    public const short PartiallyDelivered = 6;
    public const short Delivered = 7;
    public const short Closed = 8;

    /// <summary>Position in the forward-only production part of the lifecycle; Cancelled and pre-Acknowledged states are not on it.</summary>
    public static int ProductionRank(short statusId) => statusId switch
    {
        Acknowledged => 3,
        InProduction => 5,
        PartiallyDelivered => 6,
        Delivered => 7,
        Closed => 8,
        _ => 0,
    };
}
