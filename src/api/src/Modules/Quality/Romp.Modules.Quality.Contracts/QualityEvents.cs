using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Quality.Contracts;

/// <summary>
/// The shared envelope of every QC event (ADR 0004/0008): message id, type, schema version, UTC time,
/// aggregate and the PO. Payloads never carry notes, defect text or personal data (spec AC-29, AC-45).
/// </summary>
public abstract record QualityEvent(long PoId, string PoNo) : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public abstract string AggregateType { get; }

    public abstract long AggregateId { get; }

    public abstract short SchemaVersion { get; }
}

public sealed record QcLine(short SizeId, short ColourId, int Qty);

public sealed record GrnEventLine(short SizeId, short ColourId, int ClaimedQty, int CountedQty);

/// <summary>GoodsReceipt.Recorded (BRD 9.10.3). <c>ReconciliationCode</c>: Matched, Short or Over.</summary>
public sealed record GoodsReceiptRecordedEvent(
    long GrnId,
    string GrnNo,
    long RunId,
    long PoId,
    string PoNo,
    string VendorDnNo,
    DateOnly ReceivedDate,
    string ReconciliationCode,
    int ClaimedTotal,
    int CountedTotal,
    IReadOnlyList<GrnEventLine> Lines) : QualityEvent(PoId, PoNo)
{
    public override string AggregateType => "GoodsReceipt";

    public override long AggregateId => GrnId;

    public override short SchemaVersion => 1;
}

/// <summary>QC.BatchAccepted: one per Accepted or Concession sub-batch (the only trigger SKU-GEN will use, AC-24).</summary>
public sealed record QcBatchAcceptedEvent(
    long BatchId,
    long SubBatchId,
    long GrnId,
    long PoId,
    string PoNo,
    string OutcomeCode,
    IReadOnlyList<QcLine> Lines) : QualityEvent(PoId, PoNo)
{
    public override string AggregateType => "QcBatch";

    public override long AggregateId => BatchId;

    public override short SchemaVersion => 1;
}

/// <summary>QC.BatchRejected: one per rejected sub-batch.</summary>
public sealed record QcBatchRejectedEvent(
    long BatchId,
    long SubBatchId,
    long GrnId,
    long PoId,
    string PoNo,
    IReadOnlyList<QcLine> Lines) : QualityEvent(PoId, PoNo)
{
    public override string AggregateType => "QcBatch";

    public override long AggregateId => BatchId;

    public override short SchemaVersion => 1;
}

/// <summary>One per rework sub-batch sent back to the vendor (new; not in the BRD catalogue).</summary>
public sealed record QcBatchReworkRequestedEvent(
    long BatchId,
    long SubBatchId,
    long GrnId,
    long PoId,
    string PoNo,
    IReadOnlyList<QcLine> Lines) : QualityEvent(PoId, PoNo)
{
    public override string AggregateType => "QcBatch";

    public override long AggregateId => BatchId;

    public override short SchemaVersion => 1;
}

/// <summary>One per inspection round recorded (first pass and re-inspections): scorecard input (new). <c>SubBatchId</c> is 0 for the whole batch.</summary>
public sealed record QcInspectionRecordedEvent(
    long BatchId,
    long SubBatchId,
    long GrnId,
    long PoId,
    string PoNo,
    short RoundNo,
    string ModeCode,
    int LotQty,
    int SampleQty,
    int DefectiveQty,
    int CriticalCount,
    int MajorCount,
    int MinorCount,
    string ResultCode) : QualityEvent(PoId, PoNo)
{
    public override string AggregateType => "QcBatch";

    public override long AggregateId => BatchId;

    public override short SchemaVersion => 1;
}
