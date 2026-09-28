using Npgsql;

namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// Purges processed <c>INBX</c> rows past their retention window (SCRUM-93 task 14, AC-62). Talks to
/// the schema's <c>INBX</c> table over raw ADO.NET, same reasoning as <see cref="OutboxDispatcher"/>
/// - this project never depends on a module's own Infrastructure assembly (ADR 0002).
/// </summary>
public sealed class InboxRetentionPurger
{
    private readonly InboxRetentionOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The startup guard (task 14): refuses to construct - and so refuses to let the owning module
    /// start - when retention isn't longer than the max retry window, since a shorter retention
    /// could let a late redelivery slip past dedup and be handled twice.
    /// </summary>
    public InboxRetentionPurger(InboxRetentionOptions options, TimeProvider timeProvider)
    {
        if (options.RetentionPeriod <= options.MaxRetryWindow)
        {
            throw new InvalidOperationException(
                $"Inbox retention ({options.RetentionPeriod}) must be longer than the max retry window ({options.MaxRetryWindow}), or a late redelivery could be handled twice.");
        }

        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<int> PurgeAsync(string schema, CancellationToken cancellationToken)
    {
        var cutoff = _timeProvider.GetUtcNow() - _options.RetentionPeriod;

        await using var connection = new NpgsqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            $"""DELETE FROM "{schema}"."INBX" WHERE "PROC_DTE" IS NOT NULL AND "PROC_DTE" < @cutoff""",
            connection);
        command.Parameters.AddWithValue("cutoff", cutoff);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
