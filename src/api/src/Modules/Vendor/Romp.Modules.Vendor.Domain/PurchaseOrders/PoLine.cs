using Romp.BuildingBlocks.Domain;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Domain.PurchaseOrders;

/// <summary>Maps to VNDR.PO_LINE - quantity for one size x colour cell, validated against the style's size run/colourways (AC-8).</summary>
public sealed class PoLine : Entity<long>, IAuditable
{
    private PoLine()
    {
    }

    internal PoLine(long poId, short sizeId, short colourId, int qty)
    {
        PoId = poId;
        SizeId = sizeId;
        ColourId = colourId;
        Qty = qty;
    }

    public long PoId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int Qty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
