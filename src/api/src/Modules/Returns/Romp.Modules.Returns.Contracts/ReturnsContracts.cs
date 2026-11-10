using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Returns.Contracts;

/// <summary>
/// The shared envelope of every Returns event (ADR 0004, ADR 0033). Payloads carry ids, the order number, codes and quantities only: never a name,
/// address, phone or email (SEC-25). The order id is the aggregate id.
/// </summary>
public abstract record ReturnsEvent : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public string AggregateType => "ReturnCase";

    public abstract long AggregateId { get; }

    public short SchemaVersion => 1;
}

/// <summary>What reverse QC decided for one line. <see cref="DecisionCode"/> is one of <see cref="ReturnDecisionCodes"/>.</summary>
public sealed record ReturnedLineResult(long VariantId, string? Sku, string DecisionCode, int Quantity);

/// <summary>
/// Reverse QC is complete for an order's returned parcel (FR-SC-19, RQC-8). This is the only thing a refund, credit or exchange may start from;
/// nothing before it announces a result.
/// </summary>
public sealed record ReturnQcCompletedEvent(long OrderId, string OrderNumber, IReadOnlyList<ReturnedLineResult> Lines) : ReturnsEvent
{
    public override long AggregateId => OrderId;
}

/// <summary>The stable codes of a line decision, used in events and the API.</summary>
public static class ReturnDecisionCodes
{
    public const string Restock = "RESTOCK";
    public const string Seconds = "SECONDS";
    public const string Reject = "REJECT";
}
