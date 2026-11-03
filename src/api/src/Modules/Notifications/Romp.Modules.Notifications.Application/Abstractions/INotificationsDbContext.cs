using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Domain.Channels;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Application's port onto the <c>NTFY</c> database (ADR 0002): handlers depend on this, never on the Infrastructure project.
/// Sets are added here as the delivery, channels and delivery-log features arrive.
/// </summary>
public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }

    DbSet<ChannelDelivery> Deliveries { get; }

    DbSet<DeliveryAttempt> Attempts { get; }

    DbSet<DeadLetter> DeadLetters { get; }

    DbSet<MessageTemplate> Templates { get; }

    DbSet<WhatsAppEvent> WhatsAppEvents { get; }

    DbSet<BlockedNumber> BlockedNumbers { get; }

    DbSet<DailySendCount> DailyCounts { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
