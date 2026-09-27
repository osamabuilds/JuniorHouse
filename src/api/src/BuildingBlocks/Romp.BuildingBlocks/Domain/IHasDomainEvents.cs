namespace Romp.BuildingBlocks.Domain;

/// <summary>
/// Non-generic view of <see cref="AggregateRoot{TId}"/>'s domain-event list, so infrastructure
/// code (the outbox interceptor) can find every aggregate with pending events via
/// <c>ChangeTracker.Entries&lt;IHasDomainEvents&gt;()</c> without depending on any specific
/// <c>TId</c> closed generic.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
