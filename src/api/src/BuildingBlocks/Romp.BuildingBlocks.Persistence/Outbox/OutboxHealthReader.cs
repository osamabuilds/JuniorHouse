using Npgsql;

namespace Romp.BuildingBlocks.Persistence.Outbox;

/// <summary>One module's outbox at a glance (SCRUM-186): what waits, what is being delivered, what is retrying, what gave up.</summary>
public sealed record OutboxSchemaHealth(
    string Schema,
    int Pending,
    int InFlight,
    int Retrying,
    int DeadLettered,
    double? OldestPendingSeconds);

/// <summary>
/// Reads every registered schema's <c>OUTB_MSG</c> counts over raw ADO.NET, exactly like the dispatcher does (each
/// schema's table has the identical shape), so the admin app can show whether events are flowing without a module-specific query.
/// </summary>
public sealed class OutboxHealthReader(IEnumerable<OutboxModuleRegistration> registrations, OutboxDispatcherOptions options)
{
    public async Task<IReadOnlyList<OutboxSchemaHealth>> ReadAsync(CancellationToken cancellationToken)
    {
        var result = new List<OutboxSchemaHealth>();

        await using var connection = new NpgsqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var registration in registrations.OrderBy(r => r.Schema, StringComparer.Ordinal))
        {
            // The schema name comes from module registration (code), never from a request, and is quoted.
            var sql = $"""
                SELECT
                  COUNT(*) FILTER (WHERE "PROC_DTE" IS NULL AND "DEDL_IND" = false AND "ATMP_CNT" = 0
                                     AND ("CLM_BY" IS NULL OR "LEAS_EXPY_DTE" < now())),
                  COUNT(*) FILTER (WHERE "PROC_DTE" IS NULL AND "DEDL_IND" = false
                                     AND "CLM_BY" IS NOT NULL AND "LEAS_EXPY_DTE" >= now()),
                  COUNT(*) FILTER (WHERE "PROC_DTE" IS NULL AND "DEDL_IND" = false AND "ATMP_CNT" > 0
                                     AND ("CLM_BY" IS NULL OR "LEAS_EXPY_DTE" < now())),
                  COUNT(*) FILTER (WHERE "DEDL_IND" = true),
                  EXTRACT(EPOCH FROM now() - MIN("INSR_DTE") FILTER (WHERE "PROC_DTE" IS NULL AND "DEDL_IND" = false))
                FROM "{registration.Schema}"."OUTB_MSG"
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            result.Add(new OutboxSchemaHealth(
                registration.Schema,
                (int)reader.GetInt64(0),
                (int)reader.GetInt64(1),
                (int)reader.GetInt64(2),
                (int)reader.GetInt64(3),
                reader.IsDBNull(4) ? null : Convert.ToDouble(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture)));
        }

        return result;
    }
}
