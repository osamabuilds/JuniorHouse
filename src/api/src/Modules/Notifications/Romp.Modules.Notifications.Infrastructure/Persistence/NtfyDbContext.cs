using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Domain.Channels;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>NTFY</c> schema (Notifications), including the module's outbox (<c>OUTB_MSG</c>) and inbox (<c>INBX</c>).
/// Implements <see cref="INotificationsDbContext"/> (Application's own port) so handlers never depend on this project (ADR 0002).
/// </summary>
public sealed class NtfyDbContext(DbContextOptions<NtfyDbContext> options) : DbContext(options), INotificationsDbContext
{
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<ChannelDelivery> Deliveries => Set<ChannelDelivery>();

    public DbSet<DeliveryAttempt> Attempts => Set<DeliveryAttempt>();

    public DbSet<DeadLetter> DeadLetters => Set<DeadLetter>();

    public DbSet<MessageTemplate> Templates => Set<MessageTemplate>();

    public DbSet<DailySendCount> DailyCounts => Set<DailySendCount>();

    public DbSet<WhatsAppEvent> WhatsAppEvents => Set<WhatsAppEvent>();

    public DbSet<BlockedNumber> BlockedNumbers => Set<BlockedNumber>();

    async Task INotificationsDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("NTFY");
        modelBuilder.HasOutboxTable("NTFY");
        modelBuilder.HasInboxTable("NTFY");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NtfyDbContext).Assembly);
        Delivery.NotificationsLookupConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
