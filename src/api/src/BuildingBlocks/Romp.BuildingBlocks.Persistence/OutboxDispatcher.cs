using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// Talks to each registered schema's <c>OUTB_MSG</c> table over raw ADO.NET (Npgsql), not through
/// any module's own DbContext - every schema's outbox table has the identical shape
/// (<see cref="OutboxModelBuilderExtensions.HasOutboxTable"/>), so one implementation here covers
/// every module without this project depending on a single one of their Infrastructure assemblies
/// (ADR 0002). Claiming uses a short, separate transaction from delivery (AC-53, NFR-FT-08 - a
/// handler failure can never roll back the claim or the business row that wrote the message).
/// </summary>
public sealed partial class OutboxDispatcher(
    IEnumerable<OutboxModuleRegistration> registrations,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    OutboxDispatcherOptions options,
    ILogger<OutboxDispatcher> logger) : IOutboxDispatcher
{
    private static readonly string Claimant = $"{Environment.MachineName}:{Environment.ProcessId}";

    public async Task<int> DispatchOnceAsync(CancellationToken cancellationToken)
    {
        var delivered = 0;

        foreach (var registration in registrations)
        {
            delivered += await DispatchSchemaAsync(registration, cancellationToken);
        }

        return delivered;
    }

    private async Task<int> DispatchSchemaAsync(OutboxModuleRegistration registration, CancellationToken cancellationToken)
    {
        var claimed = await ClaimAsync(registration.Schema, cancellationToken);
        var delivered = 0;

        foreach (var message in claimed)
        {
            if (await DeliverAsync(registration, message, cancellationToken))
            {
                delivered++;
            }
        }

        return delivered;
    }

    private async Task<bool> DeliverAsync(OutboxModuleRegistration registration, ClaimedOutboxMessage message, CancellationToken cancellationToken)
    {
        if (!registration.EventTypesByName.TryGetValue(message.EventType, out var eventType))
        {
            LogUnregisteredEventType(message.Id, registration.Schema, message.EventType);
            return false;
        }

        var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType);
        if (domainEvent is null)
        {
            LogPayloadDeserializedToNull(message.Id, registration.Schema);
            return false;
        }

        var handlerType = typeof(IOutboxMessageHandler<>).MakeGenericType(eventType);

        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetService(handlerType);
        if (handler is null)
        {
            LogNoHandlerRegistered(eventType.Name, registration.Schema, message.Id);
            return false;
        }

        var handleMethod = handlerType.GetMethod(nameof(IOutboxMessageHandler<object>.HandleAsync))!;
        await (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;

        await MarkProcessedAsync(registration.Schema, message.Id, cancellationToken);
        return true;
    }

    private async Task<List<ClaimedOutboxMessage>> ClaimAsync(string schema, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var leaseExpiry = now.Add(options.LeaseDuration);

        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var sql = $"""
            UPDATE "{schema}"."OUTB_MSG"
            SET "CLM_BY" = @claimant, "LEAS_EXPY_DTE" = @leaseExpiry
            WHERE "ID" IN (
                SELECT "ID" FROM "{schema}"."OUTB_MSG"
                WHERE "PROC_DTE" IS NULL AND "DEDL_IND" = false
                  AND ("CLM_BY" IS NULL OR "LEAS_EXPY_DTE" < @now)
                  AND ("NXT_ATMP_DTE" IS NULL OR "NXT_ATMP_DTE" <= @now)
                ORDER BY "ID"
                FOR UPDATE SKIP LOCKED
                LIMIT @batchSize
            )
            RETURNING "ID", "EVNT_TYP", "PYLD"
            """;

        var claimed = new List<ClaimedOutboxMessage>();

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("claimant", Claimant);
            command.Parameters.AddWithValue("leaseExpiry", leaseExpiry);
            command.Parameters.AddWithValue("now", now);
            command.Parameters.AddWithValue("batchSize", options.BatchSize);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                claimed.Add(new ClaimedOutboxMessage(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    private async Task MarkProcessedAsync(string schema, long id, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"""UPDATE "{schema}"."OUTB_MSG" SET "PROC_DTE" = @now WHERE "ID" = @id""", connection);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record ClaimedOutboxMessage(long Id, string EventType, string Payload);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} in schema {Schema} has unregistered event type {EventType}; skipping.")]
    private partial void LogUnregisteredEventType(long messageId, string schema, string eventType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} in schema {Schema} deserialized to null; skipping.")]
    private partial void LogPayloadDeserializedToNull(long messageId, string schema);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No IOutboxMessageHandler<{EventType}> registered for schema {Schema}; message {MessageId} left claimed for retry.")]
    private partial void LogNoHandlerRegistered(string eventType, string schema, long messageId);
}
