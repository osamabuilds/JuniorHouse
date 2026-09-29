using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.Files;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>VNDR</c> schema (Vendor &amp; Procurement: vendors and purchase
/// orders), including the module's outbox (<c>OUTB_MSG</c>, SCRUM-165) - the only Sprint 1 module
/// that raises domain events. Implements <see cref="IVendorDbContext"/> (Application's own
/// abstraction) so Application-layer command and query handlers can use it without depending on
/// this Infrastructure project (ADR 0002).
/// </summary>
public sealed class VendorDbContext(DbContextOptions<VendorDbContext> options)
    : DbContext(options), IVendorDbContext
{
    public DbSet<Domain.Vendors.Vendor> Vendors => Set<Domain.Vendors.Vendor>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderFile> PurchaseOrderFiles => Set<PurchaseOrderFile>();

    async Task IVendorDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("VNDR");
        modelBuilder.HasOutboxTable("VNDR");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VendorDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
