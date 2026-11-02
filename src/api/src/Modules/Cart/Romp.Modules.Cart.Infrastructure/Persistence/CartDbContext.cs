using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Domain.Carts;

namespace Romp.Modules.Cart.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>CART</c> schema (Cart), including the module's outbox (<c>OUTB_MSG</c>) and inbox (<c>INBX</c>).
/// Implements <see cref="ICartDbContext"/> (Application's own port) so handlers never depend on this project (ADR 0002).
/// </summary>
public sealed class CartDbContext(DbContextOptions<CartDbContext> options) : DbContext(options), ICartDbContext
{
    public DbSet<ShoppingCart> Carts => Set<ShoppingCart>();

    async Task ICartDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("CART");
        modelBuilder.HasOutboxTable("CART");
        modelBuilder.HasInboxTable("CART");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CartDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
