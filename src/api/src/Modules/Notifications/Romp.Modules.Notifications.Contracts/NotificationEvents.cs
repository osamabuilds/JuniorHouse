using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Notifications.Contracts;

/// <summary>
/// The shared envelope of every Notifications event (ADR 0004, ADR 0026). Payloads carry ids and codes only: never a name, address,
/// phone, email or message body (ADR 0011, SEC-25). Schema changes within a version are additive.
/// </summary>
public abstract record NotificationsEvent : IOutboxEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string EventType => GetType().Name;

    public string AggregateType => "Notification";

    public abstract long AggregateId { get; }

    public short SchemaVersion => 1;
}

/// <summary>A delivery ran out of attempts and moved to the dead-letter table (DLV-3). The alert worker logs it, counts it and emails operations.</summary>
public sealed record NotificationDeadLetteredEvent(long NotificationId, string KindCode, string ChannelCode) : NotificationsEvent
{
    public override long AggregateId => NotificationId;
}

/// <summary>A delivery was handed to the provider. Audit consumes it.</summary>
public sealed record NotificationSentEvent(long NotificationId, string KindCode, string ChannelCode) : NotificationsEvent
{
    public override long AggregateId => NotificationId;
}
