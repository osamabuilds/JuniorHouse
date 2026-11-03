using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Notifications.Application.Abstractions;

namespace Romp.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>NTFY</c> schema (Notifications), including the module's outbox (<c>OUTB_MSG</c>) and inbox (<c>INBX</c>).
/// Implements <see cref="INotificationsDbContext"/> (Application's own port) so handlers never depend on this project (ADR 0002).
/// </summary>
public sealed class NtfyDbContext(DbContextOptions<NtfyDbContext> options) : DbContext(options), INotificationsDbContext
{
    async Task INotificationsDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("NTFY");
        modelBuilder.HasOutboxTable("NTFY");
        modelBuilder.HasInboxTable("NTFY");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NtfyDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
