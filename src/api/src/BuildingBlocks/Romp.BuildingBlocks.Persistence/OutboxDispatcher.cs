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

        try
        {
            await (Task)handleMethod.Invoke(handler, [domainEvent, cancellationToken])!;
        }
        catch (Exception ex)
        {
            var fault = ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex;
            await HandleFailureAsync(registration.Schema, message, fault, cancellationToken);
            return false;
        }

        await MarkProcessedAsync(registration.Schema, message.Id, cancellationToken);
        return true;
    }

    /// <summary>AC-57/AC-58: dead-letters once <see cref="OutboxDispatcherOptions.MaxAttempts"/> is reached (payload/history retained, never deleted); otherwise releases the claim and schedules the next attempt with exponential backoff + jitter.</summary>
    private async Task HandleFailureAsync(string schema, ClaimedOutboxMessage message, Exception exception, CancellationToken cancellationToken)
    {
        var attemptCount = message.AttemptCount + 1;

        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        if (attemptCount >= options.MaxAttempts)
        {
            LogDeadLettered(message.Id, schema, attemptCount, exception);

            await using var command = new NpgsqlCommand(
                $"""
                UPDATE "{schema}"."OUTB_MSG"
                SET "ATMP_CNT" = @attemptCount, "DEDL_IND" = true, "DEDL_RSN" = @reason
                WHERE "ID" = @id
                """,
                connection);
            command.Parameters.AddWithValue("attemptCount", attemptCount);
            command.Parameters.AddWithValue("reason", Truncate(exception.Message, 500));
            command.Parameters.AddWithValue("id", message.Id);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        LogDeliveryFailed(message.Id, schema, attemptCount, exception);
        var nextAttempt = timeProvider.GetUtcNow().Add(ComputeBackoff(attemptCount));

        await using var retryCommand = new NpgsqlCommand(
            $"""
            UPDATE "{schema}"."OUTB_MSG"
            SET "ATMP_CNT" = @attemptCount, "NXT_ATMP_DTE" = @nextAttempt, "CLM_BY" = NULL, "LEAS_EXPY_DTE" = NULL
            WHERE "ID" = @id
            """,
            connection);
        retryCommand.Parameters.AddWithValue("attemptCount", attemptCount);
        retryCommand.Parameters.AddWithValue("nextAttempt", nextAttempt);
        retryCommand.Parameters.AddWithValue("id", message.Id);
        await retryCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private TimeSpan ComputeBackoff(int attemptCount)
    {
        var exponential = options.RetryBaseDelay * Math.Pow(2, attemptCount - 1);
        var capped = exponential > options.RetryMaxDelay ? options.RetryMaxDelay : exponential;
        var jitterFactor = 0.5 + Random.Shared.NextDouble(); // 0.5x-1.5x, so retries don't all land in lockstep
        return capped * jitterFactor;
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

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
            RETURNING "ID", "EVNT_TYP", "PYLD", "ATMP_CNT"
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
                claimed.Add(new ClaimedOutboxMessage(
                    reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt16(3)));
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

    private sealed record ClaimedOutboxMessage(long Id, string EventType, string Payload, short AttemptCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} in schema {Schema} has unregistered event type {EventType}; skipping.")]
    private partial void LogUnregisteredEventType(long messageId, string schema, string eventType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} in schema {Schema} deserialized to null; skipping.")]
    private partial void LogPayloadDeserializedToNull(long messageId, string schema);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No IOutboxMessageHandler<{EventType}> registered for schema {Schema}; message {MessageId} left claimed for retry.")]
    private partial void LogNoHandlerRegistered(string eventType, string schema, long messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} in schema {Schema} failed on attempt {AttemptCount}; will retry with backoff.")]
    private partial void LogDeliveryFailed(long messageId, string schema, int attemptCount, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} in schema {Schema} dead-lettered after {AttemptCount} attempts.")]
    private partial void LogDeadLettered(long messageId, string schema, int attemptCount, Exception exception);
}
