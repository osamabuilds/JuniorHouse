using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Quality.Domain.InboundDeliveries;

/// <summary>What the vendor claims to have sent for one size and colour.</summary>
public sealed record ClaimedLine(short SizeId, short ColourId, int Qty);

/// <summary>
/// Maps to QC.INBD_DLVR: QC's own copy of a vendor delivery note, so the receiving queue needs no read of PROD or
/// VNDR tables (spec AC-1). Keyed by <c>(RunId, VendorDnNo)</c> because <c>DeliveryNoteRecorded</c> carries no note id.
/// </summary>
public sealed class InboundDelivery : Entity<long>, IAuditable
{
    private readonly List<InboundDeliveryLine> _lines = [];

    private InboundDelivery()
    {
        PoNo = string.Empty;
        VendorDnNo = string.Empty;
    }

    public InboundDelivery(
        long runId, long poId, string poNo, string vendorDnNo, DateOnly dispatchDate, bool isFinalShipment, IEnumerable<ClaimedLine> lines)
    {
        RunId = runId;
        PoId = poId;
        PoNo = poNo;
        VendorDnNo = vendorDnNo;
        DispatchDate = dispatchDate;
        IsFinalShipment = isFinalShipment;
        _lines.AddRange(lines.Select(l => new InboundDeliveryLine(l.SizeId, l.ColourId, l.Qty)));
    }

    public long RunId { get; private set; }

    public long PoId { get; private set; }

    public string PoNo { get; private set; }

    public string VendorDnNo { get; private set; }

    public DateOnly DispatchDate { get; private set; }

    public bool IsFinalShipment { get; private set; }

    /// <summary>Set when the PO is cancelled (AC-5): no goods receipt can be recorded against it.</summary>
    public bool IsCancelled { get; private set; }

    public IReadOnlyCollection<InboundDeliveryLine> Lines => _lines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public void MarkCancelled() => IsCancelled = true;
}

/// <summary>Maps to QC.INBD_DLVR_LINE.</summary>
public sealed class InboundDeliveryLine : Entity<long>, IAuditable
{
    private InboundDeliveryLine()
    {
    }

    internal InboundDeliveryLine(short sizeId, short colourId, int claimedQty)
    {
        SizeId = sizeId;
        ColourId = colourId;
        ClaimedQty = claimedQty;
    }

    public long DeliveryId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int ClaimedQty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
