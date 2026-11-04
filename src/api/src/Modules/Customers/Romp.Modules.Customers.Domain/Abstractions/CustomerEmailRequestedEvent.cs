using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Customers.Domain.Abstractions;

/// <summary>Which customer email to send. Stored as text in the event; the name is part of the event contract, so never rename a value.</summary>
public enum CustomerEmailKind
{
    NewsletterWelcome = 1,
    RestockNotice = 2,
}

/// <summary>
/// "Send this customer email" (ADR 0022): written to the outbox in the same transaction as the change that needs it. It carries the kind
/// and the id of the record to read at send time and nothing else: no address, no name, no body (ADR 0011, SEC-25). A later Notifications
/// subscriber can take over without the raising code changing (ADR 0026, task X1).
/// </summary>
public sealed record CustomerEmailRequestedEvent(CustomerEmailKind Kind, long RelatedId) : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string AggregateType => Kind.ToString();

    public long AggregateId => RelatedId;

    public short SchemaVersion => 1;
}
