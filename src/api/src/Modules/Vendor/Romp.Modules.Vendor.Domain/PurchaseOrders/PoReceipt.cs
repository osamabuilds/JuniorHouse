using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain.PurchaseOrders;

/// <summary>What was claimed and what was counted for one size and colour on a goods receipt.</summary>
public sealed record ReceiptLineInput(short SizeId, short ColourId, int ClaimedQty, int CountedQty);

/// <summary>
/// Maps to VNDR.PO_RCVD: VNDR's record that a goods receipt was made against the PO (Sprint 4, spec AC-40). It is receipt
/// progress only: it never changes the PO status (the recommended default for Q7; closing a PO stays manual, Sprint 3 D2).
/// One row per goods receipt, unique by the GRN id, so a redelivered event is a no-op (AC-41).
/// </summary>
public sealed class PoReceipt : Entity<long>, IAuditable
{
    private readonly List<PoReceiptLine> _lines = [];

    private PoReceipt()
    {
        GrnNo = string.Empty;
    }

    public PoReceipt(long poId, long grnId, string grnNo, DateOnly receivedDate, IEnumerable<ReceiptLineInput> lines)
    {
        PoId = poId;
        GrnId = grnId;
        GrnNo = grnNo;
        ReceivedDate = receivedDate;
        _lines.AddRange(lines.Select(l => new PoReceiptLine(l.SizeId, l.ColourId, l.ClaimedQty, l.CountedQty)));
        ClaimedTotal = _lines.Sum(l => l.ClaimedQty);
        CountedTotal = _lines.Sum(l => l.CountedQty);
    }

    public long PoId { get; private set; }

    /// <summary>QC goods receipt id: a plain reference, never a foreign key across schemas.</summary>
    public long GrnId { get; private set; }

    public string GrnNo { get; private set; }

    public DateOnly ReceivedDate { get; private set; }

    public int ClaimedTotal { get; private set; }

    public int CountedTotal { get; private set; }

    public IReadOnlyCollection<PoReceiptLine> Lines => _lines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to VNDR.PO_RCVD_LINE.</summary>
public sealed class PoReceiptLine : Entity<long>, IAuditable
{
    private PoReceiptLine()
    {
    }

    internal PoReceiptLine(short sizeId, short colourId, int claimedQty, int countedQty)
    {
        SizeId = sizeId;
        ColourId = colourId;
        ClaimedQty = claimedQty;
        CountedQty = countedQty;
    }

    public long ReceiptId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int ClaimedQty { get; private set; }

    public int CountedQty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
