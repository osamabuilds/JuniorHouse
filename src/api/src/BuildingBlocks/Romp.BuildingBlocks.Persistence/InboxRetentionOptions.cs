namespace Romp.BuildingBlocks.Persistence;

/// <summary>
/// Tuning for a module's own <c>INBX</c> retention purge (SCRUM-93 task 14). Not wired to any real
/// module this sprint - task 12's inbox convention has no real consumer yet - built now so Sprint
/// 3's first real consumer gets retention and the startup guard below for free.
/// </summary>
public sealed class InboxRetentionOptions
{
    public required string ConnectionString { get; init; }

    /// <summary>How long a processed INBX row is kept before it becomes eligible for purge.</summary>
    public required TimeSpan RetentionPeriod { get; init; }

    /// <summary>
    /// The consuming module's outbox dispatcher's worst-case span between a message's first attempt
    /// and its last possible redelivery (retries plus lease expiry). Retention must exceed this, or
    /// a very late redelivery could find its dedup row already purged and be handled twice.
    /// </summary>
    public required TimeSpan MaxRetryWindow { get; init; }
}
