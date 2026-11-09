namespace Romp.Modules.Notifications.Domain.Delivery;

/// <summary>
/// Standard lookup shape (docs/db/naming.md): the system-owned rows behind each enumerated value, seeded by migration. The key has the
/// same type as the value it describes (the enum, or the reference-kind code) so the foreign keys from the tables line up; the database
/// stores it as a smallint.
/// </summary>
public abstract class NotificationsLookup<TKey>
{
    public TKey Id { get; set; } = default!;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public short SortSeq { get; set; }

    public bool IsActive { get; set; }
}

public sealed class NotificationKindLookup : NotificationsLookup<NotificationKind>;

public sealed class ChannelLookup : NotificationsLookup<NotificationChannel>;

public sealed class DeliveryStatusLookup : NotificationsLookup<DeliveryStatus>;

public sealed class ErrorCategoryLookup : NotificationsLookup<ErrorCategory>;

/// <summary>Keyed by the reference code (<see cref="ReferenceKinds"/>).</summary>
public sealed class ReferenceKindLookup : NotificationsLookup<string>;
