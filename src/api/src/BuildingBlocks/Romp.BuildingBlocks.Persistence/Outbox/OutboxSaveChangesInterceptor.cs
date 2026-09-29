using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Romp.BuildingBlocks.Domain;

namespace Romp.BuildingBlocks.Persistence.Outbox;

/// <summary>
/// Moves every pending domain event off each changed aggregate (<see cref="IHasDomainEvents"/>)
/// into an <see cref="OutboxMessage"/> row, as part of the same <c>SaveChanges</c> call as the
/// business change that raised it - so the two either both commit or both roll back (ADR 0004).
/// Only a DbContext that has called <see cref="OutboxModelBuilderExtensions.HasOutboxTable"/> can
/// use this interceptor; it resolves <c>OUTB_MSG</c> via <see cref="DbContext.Set{TEntity}"/>.
/// </summary>
public sealed class OutboxSaveChangesInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Enqueue(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Enqueue(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Enqueue(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var aggregatesWithEvents = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .ToList();

        foreach (var entry in aggregatesWithEvents)
        {
            foreach (var domainEvent in entry.Entity.DomainEvents)
            {
                var envelope = domainEvent as IOutboxEvent;
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    EventType = domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    InsrDte = now,
                    AggregateType = envelope?.AggregateType ?? "PurchaseOrder",
                    AggregateId = envelope is { AggregateId: > 0 } ? envelope.AggregateId : null,
                    MessageVersion = envelope?.SchemaVersion ?? 1,
                });
            }

            entry.Entity.ClearDomainEvents();
        }
    }
}
