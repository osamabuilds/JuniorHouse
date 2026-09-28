namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// A module implements this once per domain event type it wants delivered once the event is
/// durably committed to its own OUTB_MSG table (ADR 0004/0007). Registered in DI like any other
/// service (typically <c>services.AddScoped&lt;IOutboxMessageHandler&lt;TEvent&gt;, ...&gt;()</c>);
/// <see cref="OutboxDispatcher"/> resolves the closed generic by the event's CLR type at delivery
/// time - it never needs to know about a specific event type at compile time.
/// </summary>
public interface IOutboxMessageHandler<in TEvent>
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
