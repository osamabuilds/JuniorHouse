namespace Romp.BuildingBlocks.Domain;

/// <summary>
/// A domain event that carries the integration envelope the outbox dispatcher needs (SCRUM-93
/// task 44, ADR 0007): which aggregate it belongs to (per-aggregate ordering) and which schema
/// version its payload is. <see cref="IDomainEvent.EventId"/> is the message id and
/// <see cref="IDomainEvent.OccurredAt"/> the UTC occurrence time.
/// </summary>
public interface IOutboxEvent : IDomainEvent
{
    string AggregateType { get; }

    /// <summary>Zero when the aggregate's database id doesn't exist yet (a brand-new row) - the outbox then leaves the column empty rather than record a wrong id.</summary>
    long AggregateId { get; }

    short SchemaVersion { get; }
}
