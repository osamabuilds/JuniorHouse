namespace Romp.BuildingBlocks.Persistence.Outbox;

/// <summary>
/// A durably-persisted domain event, written by <see cref="OutboxSaveChangesInterceptor"/> in the
/// same transaction as the business change that raised it (ADR 0004). Sprint 1 wrote rows only -
/// no dispatcher/consumer existed yet (SCRUM-165's Sprint 1 scope note). The dispatcher/lease/
/// retry/dead-letter columns below arrive with SCRUM-181's dispatcher (ADR 0007 refines ADR 0004);
/// they're additive here (task 6) - <see cref="OutboxSaveChangesInterceptor"/> and the dispatcher
/// that reads/claims them are task 7+.
/// </summary>
public sealed class OutboxMessage
{
    public long Id { get; init; }

    public required string EventType { get; init; }

    /// <summary>The domain event, serialized as JSON.</summary>
    public required string Payload { get; init; }

    public DateTimeOffset InsrDte { get; init; }

    /// <summary>Defaults to "PurchaseOrder" (Sprint 1's only aggregate type) so existing rows backfill cleanly.</summary>
    public string AggregateType { get; init; } = "PurchaseOrder";

    public long? AggregateId { get; init; }

    public short MessageVersion { get; init; } = 1;

    public short AttemptCount { get; init; }

    public DateTimeOffset? NextAttemptDte { get; init; }

    public string? ClaimedBy { get; init; }

    public DateTimeOffset? LeaseExpiryDte { get; init; }

    public DateTimeOffset? ProcessedDte { get; init; }

    public bool IsDeadLettered { get; init; }

    public string? DeadLetterReason { get; init; }
}
