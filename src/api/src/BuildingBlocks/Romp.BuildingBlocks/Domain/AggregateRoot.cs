namespace Romp.BuildingBlocks.Domain;

/// <summary>
/// Consistency boundary. Domain events raised here are persisted to the transactional outbox
/// in the same database transaction as the aggregate (BRD §9.5, §9.10).
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
