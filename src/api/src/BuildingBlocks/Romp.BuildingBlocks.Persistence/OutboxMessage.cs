namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// A durably-persisted domain event, written by <see cref="OutboxSaveChangesInterceptor"/> in the
/// same transaction as the business change that raised it (ADR 0004). Writer only this sprint -
/// no dispatcher/consumer exists yet (SCRUM-165's Sprint 1 scope note), so there is deliberately
/// no processing-status column here; that arrives with the dispatcher in a later sprint.
/// </summary>
public sealed class OutboxMessage
{
    public long Id { get; init; }

    public required string EventType { get; init; }

    /// <summary>The domain event, serialized as JSON.</summary>
    public required string Payload { get; init; }

    public DateTimeOffset InsrDte { get; init; }
}
