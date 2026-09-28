using Microsoft.Extensions.Logging;

namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// Placeholder handler for an outbox event that has no real consumer yet (SCRUM-93 task 13). Only
/// logs, so the dispatcher still marks the row Processed instead of leaving it stuck as
/// "no handler registered" (<see cref="OutboxDispatcher"/>). Has no database effect, so it needs no
/// <see cref="InboxMessage"/> row - a module registers this generically for its own event types via
/// <c>services.AddScoped(typeof(IOutboxMessageHandler&lt;&gt;), typeof(LoggingOutboxMessageHandler&lt;&gt;))</c>
/// and replaces it per event type once it adds a real handler with a side effect.
/// </summary>
public sealed partial class LoggingOutboxMessageHandler<TEvent>(ILogger<LoggingOutboxMessageHandler<TEvent>> logger)
    : IOutboxMessageHandler<TEvent>
{
    public Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken)
    {
        LogReceived(typeof(TEvent).Name);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox event {EventType} processed by placeholder logging handler.")]
    private partial void LogReceived(string eventType);
}
