using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>ORDR</c> schema (Orders), including the module's outbox (<c>OUTB_MSG</c>) and inbox (<c>INBX</c>).
/// Implements <see cref="IOrdersDbContext"/> (Application's own port) so handlers never depend on this project (ADR 0002).
/// </summary>
public sealed class OrdrDbContext(DbContextOptions<OrdrDbContext> options) : DbContext(options), IOrdersDbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<OrderHistory> OrderHistory => Set<OrderHistory>();

    async Task IOrdersDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ORDR");
        modelBuilder.HasOutboxTable("ORDR");
        modelBuilder.HasInboxTable("ORDR");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdrDbContext).Assembly);
        OrdersLookupConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
