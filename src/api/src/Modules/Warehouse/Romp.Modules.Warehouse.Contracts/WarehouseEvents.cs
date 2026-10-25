using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Warehouse.Contracts;

/// <summary>
/// The shared envelope of every Warehouse event (ADR 0004, ADR 0012). Payloads carry ids, SKU, GTIN and quantities only: no notes,
/// staff names or personal data. Schema changes within a version are additive (ADR 0011). The two stock events carry the variant's stock
/// version (0 when the sender predates it).
/// </summary>
public abstract record WarehouseEvent : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public abstract string AggregateType { get; }

    public abstract long AggregateId { get; }

    public abstract short SchemaVersion { get; }
}

/// <summary>Stock of a variant was shelved and is now available (FR-SC-08, §9.10.3 <c>Stock.PutawayConfirmed</c>).</summary>
public sealed record StockPutawayConfirmedEvent(
    long VariantId,
    long StyleId,
    short ColourId,
    short SizeId,
    string SkuCode,
    string Gtin,
    string GradeCode,
    int Quantity,
    int AvailableAfter,
    long Version = 0) : WarehouseEvent
{
    public override string AggregateType => "Variant";

    public override long AggregateId => VariantId;

    public override short SchemaVersion => 1;
}

/// <summary>The on-hand quantity of a variant changed (putaway, adjustment, count). One event per variant per transaction (ADR 0012).</summary>
public sealed record StockLevelChangedEvent(
    long VariantId,
    long StyleId,
    short ColourId,
    short SizeId,
    string SkuCode,
    string Gtin,
    int OnHand,
    int Available,
    int OnHandAccepted,
    int OnHandConcession,
    string ReasonCode,
    long Version = 0) : WarehouseEvent
{
    public override string AggregateType => "Variant";

    public override long AggregateId => VariantId;

    public override short SchemaVersion => 1;
}

/// <summary>A low-stock or stock-out alert opened or escalated (FR-54). Delivery channels are a later feature (D6).</summary>
public sealed record StockAlertRaisedEvent(
    long VariantId,
    string SkuCode,
    string LevelCode,
    int Available,
    int Threshold) : WarehouseEvent
{
    public override string AggregateType => "Variant";

    public override long AggregateId => VariantId;

    public override short SchemaVersion => 1;
}
