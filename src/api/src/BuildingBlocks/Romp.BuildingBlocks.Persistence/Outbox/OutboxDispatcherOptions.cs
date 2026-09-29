namespace Romp.BuildingBlocks.Persistence.Outbox;

/// <summary>Tuning for the shared outbox dispatcher (SCRUM-181). Defaults are dev-friendly; production values come from configuration.</summary>
public sealed class OutboxDispatcherOptions
{
    /// <summary>
    /// The same "Postgres" connection string every module's own RegisterServices reads via
    /// IConfiguration.GetConnectionString - passed in explicitly here (rather than this project
    /// taking a dependency on Microsoft.Extensions.Configuration.Abstractions itself) since the
    /// dispatcher talks to each registered schema's OUTB_MSG table directly over ADO.NET, not
    /// through any module's own DbContext.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>How often <see cref="OutboxDispatcherHostedService"/> calls <see cref="IOutboxDispatcher.DispatchOnceAsync"/>.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Max rows claimed per schema per pass.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>How long a claim is honoured before another dispatcher instance may reclaim the row (task 8 - lease expiry).</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>A message is dead-lettered once its attempt count reaches this (task 9, AC-57/AC-58).</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Exponential backoff base: attempt N waits roughly <c>RetryBaseDelay * 2^N</c> (capped at <see cref="RetryMaxDelay"/>), jittered +/-50% to avoid every failed message retrying in lockstep.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(30);
}
