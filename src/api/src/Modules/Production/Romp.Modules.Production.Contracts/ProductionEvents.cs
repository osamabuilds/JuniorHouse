using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Production.Contracts;

/// <summary>
/// The shared envelope of every PROD event (ADR 0004/0008): message id, type, schema version, UTC
/// time, aggregate (the production run) and <c>PoNo</c>. Payloads never carry internal notes, files
/// or reporter contact details (spec AC-30).
/// </summary>
public abstract record ProductionEvent(long RunId, long PoId, string PoNo) : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public string AggregateType => "ProductionRun";

    public long AggregateId => RunId;

    public abstract short SchemaVersion { get; }
}

public sealed record ProductionRunOpenedEvent(long RunId, long PoId, string PoNo) : ProductionEvent(RunId, PoId, PoNo)
{
    public override short SchemaVersion => 1;
}

/// <summary>Production.MilestoneReached (BRD §9.10.3). <c>MilestoneCode</c> is the stable lookup code.</summary>
public sealed record ProductionMilestoneReachedEvent(long RunId, long PoId, string PoNo, string MilestoneCode, DateOnly ClaimedDate)
    : ProductionEvent(RunId, PoId, PoNo)
{
    public override short SchemaVersion => 1;
}

/// <summary>Bulk cutting was recorded (D1); VNDR moves the PO to In Production.</summary>
public sealed record ProductionStartedEvent(long RunId, long PoId, string PoNo, DateOnly ClaimedDate) : ProductionEvent(RunId, PoId, PoNo)
{
    public override short SchemaVersion => 1;
}

public sealed record DeliveredLine(short SizeId, short ColourId, int Qty);

/// <summary>
/// A delivery note was recorded with PROD's judgement (spec AC-31). <c>PoOutcome</c> is
/// <c>PartiallyDelivered</c> or <c>Delivered</c>; VNDR applies it forward-only. Sprint 4's scorecard reads the facts.
/// </summary>
public sealed record DeliveryNoteRecordedEvent(
    long RunId,
    long PoId,
    string PoNo,
    string VendorDnNo,
    DateOnly DispatchDate,
    bool IsFinalShipment,
    string PoOutcome,
    string TimingCode,
    int DaysLate,
    string QuantityCode,
    int OverQty,
    int ShortQty,
    short RevisionNumber,
    bool RompFabricDelay,
    IReadOnlyList<DeliveredLine> Lines) : ProductionEvent(RunId, PoId, PoNo)
{
    public override short SchemaVersion => 1;
}

public sealed record ProductionRunCancelledEvent(long RunId, long PoId, string PoNo) : ProductionEvent(RunId, PoId, PoNo)
{
    public override short SchemaVersion => 1;
}

public sealed record ProductionRunClosedEvent(long RunId, long PoId, string PoNo) : ProductionEvent(RunId, PoId, PoNo)
{
    public override short SchemaVersion => 1;
}
