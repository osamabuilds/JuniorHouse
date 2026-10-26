using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>CUST</c> schema (Customers), including the module's outbox (<c>OUTB_MSG</c>) and inbox (<c>INBX</c>).
/// Implements <see cref="ICustomerDbContext"/> (Application's own port) so handlers never depend on this project (ADR 0002).
/// </summary>
public sealed class CustDbContext(DbContextOptions<CustDbContext> options) : DbContext(options), ICustomerDbContext
{
    public DbSet<Customer> Customers => Set<Customer>();

    async Task ICustomerDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("CUST");
        modelBuilder.HasOutboxTable("CUST");
        modelBuilder.HasInboxTable("CUST");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
