using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Storefront.Contracts;

/// <summary>
/// The shared envelope of every Storefront event (ADR 0004). Payloads carry ids, versions and hashes only: no settings content, no
/// staff names (ADR 0011). Schema changes within a version are additive.
/// </summary>
public abstract record StorefrontEvent : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public abstract string AggregateType { get; }

    public abstract long AggregateId { get; }

    public abstract short SchemaVersion { get; }
}

/// <summary>A new settings snapshot became the current one (publish or restore). Consumers drop their cached copy (ADR 0020).</summary>
public sealed record StorefrontSettingsPublishedEvent(int VersionNo, string DocumentHash) : StorefrontEvent
{
    public override string AggregateType => "SettingsSnapshot";

    public override long AggregateId => VersionNo;

    public override short SchemaVersion => 1;
}
