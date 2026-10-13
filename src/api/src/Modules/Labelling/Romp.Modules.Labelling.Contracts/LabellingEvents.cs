using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Labelling.Contracts;

/// <summary>
/// The shared envelope of every Labelling event (ADR 0004). Payloads carry ids, SKU and GTIN only: no notes, staff names or
/// personal data. Schema changes within a version are additive (ADR 0011).
/// </summary>
public abstract record LabellingEvent : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public abstract string AggregateType { get; }

    public abstract long AggregateId { get; }

    public abstract short SchemaVersion { get; }
}

/// <summary>A variant (style × colour × size) received its GTIN-13 and SKU code (FR-SC-06).</summary>
public sealed record VariantGtinAssignedEvent(
    long VariantId,
    long StyleId,
    short ColourId,
    short SizeId,
    string SkuCode,
    string Gtin) : LabellingEvent
{
    public override string AggregateType => "Variant";

    public override long AggregateId => VariantId;

    public override short SchemaVersion => 1;
}

/// <summary>A GTIN was retired and will never be issued again (NFR-SC-03). <c>ReasonCode</c>: Error, Recalled or Other.</summary>
public sealed record GtinVoidedEvent(
    long VariantId,
    long StyleId,
    short ColourId,
    short SizeId,
    string SkuCode,
    string Gtin,
    string ReasonCode) : LabellingEvent
{
    public override string AggregateType => "Variant";

    public override long AggregateId => VariantId;

    public override short SchemaVersion => 1;
}

/// <summary>A tag print job finished printing. <c>KindCode</c>: Original or Reprint.</summary>
public sealed record TagJobPrintedEvent(
    long JobId,
    string JobNo,
    string SkuCode,
    string Gtin,
    int Qty,
    string KindCode) : LabellingEvent
{
    public override string AggregateType => "PrintJob";

    public override long AggregateId => JobId;

    public override short SchemaVersion => 1;
}
