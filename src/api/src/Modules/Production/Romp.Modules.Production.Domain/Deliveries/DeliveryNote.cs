using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Production.Domain.Deliveries;

/// <summary>Maps to PROD.DLVR_NOTE: a vendor delivery note against the run's PO (FR-SC-04, part 1).</summary>
public sealed class DeliveryNote : Entity<long>, IAuditable
{
    private readonly List<DeliveryNoteLine> _lines = [];

    private DeliveryNote()
    {
        VendorDnNo = string.Empty;
        ActingPerson = string.Empty;
    }

    internal DeliveryNote(
        long runId, string vendorDnNo, DateOnly dispatchDate, bool isFinalShipment, short revisionNumber,
        DeliveryJudgement judgement, DateOnly? fabricDispatchDate, bool rompFabricDelay, string actingPerson)
    {
        RunId = runId;
        VendorDnNo = vendorDnNo;
        DispatchDate = dispatchDate;
        IsFinalShipment = isFinalShipment;
        RevisionNumber = revisionNumber;
        TimingId = judgement.TimingId;
        DaysLate = judgement.DaysLate;
        QuantityStatusId = judgement.QuantityStatusId;
        OverQty = judgement.OverQty;
        ShortQty = judgement.ShortQty;
        FabricDispatchDate = fabricDispatchDate;
        IsRompFabricDelay = rompFabricDelay;
        ActingPerson = actingPerson;
        _lines.AddRange(judgement.Lines.Select(l => new DeliveryNoteLine(l.SizeId, l.ColourId, l.Qty, l.OverQty, l.ShortQty)));
    }

    public long RunId { get; private set; }

    /// <summary>The vendor's own DN number; unique per run (AC-20).</summary>
    public string VendorDnNo { get; private set; }

    /// <summary>The actual dispatch date, which the note is judged on (D12), not the entry date.</summary>
    public DateOnly DispatchDate { get; private set; }

    /// <summary>D3: staff marked this as the vendor's last shipment.</summary>
    public bool IsFinalShipment { get; private set; }

    /// <summary>The PO revision in force on <see cref="DispatchDate"/> that this note was judged against.</summary>
    public short RevisionNumber { get; private set; }

    public short TimingId { get; private set; }

    public int DaysLate { get; private set; }

    public short QuantityStatusId { get; private set; }

    public int OverQty { get; private set; }

    public int ShortQty { get; private set; }

    /// <summary>D14 / AC-37: when Romp dispatched the fabric (RompSupplied runs), so Sprint 4 can exclude Romp-caused delay.</summary>
    public DateOnly? FabricDispatchDate { get; private set; }

    public bool IsRompFabricDelay { get; private set; }

    public string ActingPerson { get; private set; }

    public IReadOnlyCollection<DeliveryNoteLine> Lines => _lines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to PROD.DLVR_NOTE_LINE.</summary>
public sealed class DeliveryNoteLine : Entity<long>, IAuditable
{
    private DeliveryNoteLine()
    {
    }

    internal DeliveryNoteLine(short sizeId, short colourId, int qty, int overQty, int shortQty)
    {
        SizeId = sizeId;
        ColourId = colourId;
        Qty = qty;
        OverQty = overQty;
        ShortQty = shortQty;
    }

    public long NoteId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int Qty { get; private set; }

    public int OverQty { get; private set; }

    public int ShortQty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
