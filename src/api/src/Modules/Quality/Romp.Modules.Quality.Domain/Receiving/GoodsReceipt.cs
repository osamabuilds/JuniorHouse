using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Quality.Domain.InboundDeliveries;

namespace Romp.Modules.Quality.Domain.Receiving;

/// <summary>What staff counted for one size and colour.</summary>
public sealed record CountedLine(short SizeId, short ColourId, int Qty);

/// <summary>Maps to QC.GRN_MAIN: a goods receipt note recorded against one inbound delivery (FR-SC-04, BRD 5.11).</summary>
public sealed class GoodsReceipt : AggregateRoot<long>, IAuditable
{
    private readonly List<GoodsReceiptLine> _lines = [];

    private GoodsReceipt()
    {
        GrnNo = string.Empty;
        PoNo = string.Empty;
        VendorDnNo = string.Empty;
        ActingPerson = string.Empty;
    }

    public string GrnNo { get; private set; }

    public long DeliveryId { get; private set; }

    public long RunId { get; private set; }

    public long PoId { get; private set; }

    public string PoNo { get; private set; }

    public string VendorDnNo { get; private set; }

    public DateOnly ReceivedDate { get; private set; }

    public short StatusId { get; private set; }

    public short ReconciliationId { get; private set; }

    public int ClaimedTotal { get; private set; }

    public int CountedTotal { get; private set; }

    public string ActingPerson { get; private set; }

    public string? Note { get; private set; }

    public IReadOnlyCollection<GoodsReceiptLine> Lines => _lines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>
    /// Opens a goods receipt for <paramref name="delivery"/> (spec AC-3..AC-8). Every claimed line gets a row (uncounted
    /// means counted 0); a counted line that is not on the delivery must be on the PO's in-force terms
    /// (<paramref name="poLines"/>) and is recorded as fully excess. The reconciliation is computed, never typed (AC-7).
    /// </summary>
    public static GoodsReceipt Open(
        InboundDelivery delivery,
        string grnNo,
        DateOnly receivedDate,
        string actingPerson,
        string? note,
        IReadOnlyList<CountedLine> counted,
        IReadOnlySet<(short SizeId, short ColourId)> poLines,
        DateOnly today,
        int futureDays)
    {
        if (delivery.IsCancelled)
        {
            throw new DomainException($"Delivery note {delivery.VendorDnNo} belongs to a cancelled purchase order ({delivery.PoNo}), so no goods receipt can be recorded.");
        }

        var name = actingPerson?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 100)
        {
            throw new DomainException("Enter the name of the person recording this (2 to 100 characters).");
        }

        if (receivedDate < delivery.DispatchDate)
        {
            throw new DomainException($"The receipt date cannot be before the delivery note's dispatch date ({delivery.DispatchDate:yyyy-MM-dd}).");
        }

        if (receivedDate > today.AddDays(futureDays))
        {
            throw new DomainException($"The receipt date cannot be after today ({today:yyyy-MM-dd}).");
        }

        var countedByLine = new Dictionary<(short, short), int>();
        foreach (var line in counted)
        {
            if (line.Qty < 0)
            {
                throw new DomainException($"The counted quantity for size {line.SizeId} and colour {line.ColourId} must be a whole number of 0 or more.");
            }

            if (!countedByLine.TryAdd((line.SizeId, line.ColourId), line.Qty))
            {
                throw new DomainException($"Size {line.SizeId} and colour {line.ColourId} appears more than once; enter one counted quantity per line.");
            }
        }

        var claimedByLine = delivery.Lines.ToDictionary(l => (l.SizeId, l.ColourId), l => l.ClaimedQty);
        foreach (var key in countedByLine.Keys)
        {
            if (!claimedByLine.ContainsKey(key) && !poLines.Contains(key))
            {
                throw new DomainException($"Size {key.Item1} and colour {key.Item2} is not on delivery note {delivery.VendorDnNo} or on purchase order {delivery.PoNo}.");
            }
        }

        var judged = ReconciliationJudge.Judge(
            claimedByLine.Keys.Union(countedByLine.Keys)
                .OrderBy(k => k.Item1).ThenBy(k => k.Item2)
                .Select(k => new ReconciliationLine(
                    k.Item1, k.Item2,
                    claimedByLine.GetValueOrDefault(k),
                    countedByLine.GetValueOrDefault(k)))
                .ToList());

        var receipt = new GoodsReceipt
        {
            GrnNo = grnNo,
            DeliveryId = delivery.Id,
            RunId = delivery.RunId,
            PoId = delivery.PoId,
            PoNo = delivery.PoNo,
            VendorDnNo = delivery.VendorDnNo,
            ReceivedDate = receivedDate,
            StatusId = GrnStatus.Recorded,
            ReconciliationId = judged.StatusId,
            ClaimedTotal = judged.ClaimedTotal,
            CountedTotal = judged.CountedTotal,
            ActingPerson = name,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
        receipt._lines.AddRange(judged.Lines.Select(l => new GoodsReceiptLine(l)));
        return receipt;
    }

    /// <summary>
    /// Raises <c>GoodsReceipt.Recorded</c> (AC-10). Call after the row has been saved once, so the event carries the id;
    /// the caller's transaction keeps the receipt and the event atomic.
    /// </summary>
    public void RaiseRecorded() => Raise(new GoodsReceiptRecordedEvent(
        Id, GrnNo, RunId, PoId, PoNo, VendorDnNo, ReceivedDate, GrnReconciliation.Code(ReconciliationId), ClaimedTotal, CountedTotal,
        _lines.Select(l => new GrnEventLine(l.SizeId, l.ColourId, l.ClaimedQty, l.CountedQty)).ToList()));
}

/// <summary>Maps to QC.GRN_LINE: claimed against counted for one size and colour.</summary>
public sealed class GoodsReceiptLine : Entity<long>, IAuditable
{
    private GoodsReceiptLine()
    {
    }

    internal GoodsReceiptLine(ReconciliationLine line)
    {
        SizeId = line.SizeId;
        ColourId = line.ColourId;
        ClaimedQty = line.ClaimedQty;
        CountedQty = line.CountedQty;
        DiffQty = line.DiffQty;
        IsExcess = line.IsExcess;
    }

    public long GrnId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int ClaimedQty { get; private set; }

    public int CountedQty { get; private set; }

    /// <summary>Counted minus claimed: negative is short, positive is over.</summary>
    public int DiffQty { get; private set; }

    public bool IsExcess { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to QC.GRN_NO_SEQ: the per-year counter behind <c>GRN-YYYY-NNNNN</c> (ADR 0006), private to QC.</summary>
public sealed class GrnNumberSequence
{
    private GrnNumberSequence()
    {
    }

    public short Year { get; private set; }

    public int LastSeq { get; private set; }
}
