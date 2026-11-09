using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Fulfilment.Contracts;

/// <summary>
/// The shared envelope of every Fulfilment event (ADR 0004, ADR 0030). Payloads carry ids, the order number and codes only: never a name,
/// address, phone or email (SEC-25). Notifications loads what it needs when it sends. Schema changes within a version are additive.
/// </summary>
public abstract record FulfilmentEvent : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public string AggregateType => "FulfilmentOrder";

    public abstract long AggregateId { get; }

    public short SchemaVersion => 1;
}

/// <summary>Every unit of an order was scanned and the stock decremented (PCK-7).</summary>
public sealed record PickCompletedEvent(long OrderId, string OrderNumber) : FulfilmentEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>
/// A line of a confirmed order cannot be shipped yet (a pick exception or a shortage at allocation): the customer is told (PEX-3, Q-FUL-5).
/// <see cref="ReasonCode"/> is one of <c>NOT_FOUND</c>, <c>SHORT</c>, <c>DAMAGED</c> or <c>NO_STOCK</c>.
/// </summary>
public sealed record OrderLineDelayedEvent(long OrderId, string OrderNumber, long VariantId, string ReasonCode) : FulfilmentEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>A picker flagged a stock mismatch at a bin: Warehouse marks the bin "under review" so it is not allocated from until counted (PEX-6).</summary>
public sealed record PickExceptionRaisedEvent(long OrderId, string OrderNumber, long BinId, string BinCode, string ReasonCode) : FulfilmentEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>The parcel is sealed and weighed; S15 books the courier from it (PAK-9). The weight feeds the courier's rate.</summary>
public sealed record OrderPackedEvent(long OrderId, string OrderNumber, int WeightGrams) : FulfilmentEvent
{
    public override long AggregateId => OrderId;
}
