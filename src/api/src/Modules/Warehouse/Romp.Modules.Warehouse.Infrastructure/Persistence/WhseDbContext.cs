using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Domain.Adjustments;
using Romp.Modules.Warehouse.Domain.Bins;
using Romp.Modules.Warehouse.Domain.StockAlerts;
using Romp.Modules.Warehouse.Domain.CycleCounts;
using Romp.Modules.Warehouse.Domain.Putaway;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the <c>WHSE</c> schema (bins, stock ledger, putaway, adjustments, counts, alerts), including the module's
/// outbox and inbox. Implements <see cref="IWarehouseDbContext"/> (Application's own port) so handlers never depend on this project (ADR 0002).
/// </summary>
public sealed class WhseDbContext(DbContextOptions<WhseDbContext> options) : DbContext(options), IWarehouseDbContext
{
    public DbSet<Bin> Bins => Set<Bin>();

    public DbSet<VariantRef> VariantRefs => Set<VariantRef>();

    public DbSet<StockBalance> Balances => Set<StockBalance>();

    public DbSet<StockMovement> Movements => Set<StockMovement>();

    public DbSet<PutawayItem> PutawayItems => Set<PutawayItem>();

    public DbSet<PutawayRecord> PutawayRecords => Set<PutawayRecord>();

    public DbSet<StockAdjustment> Adjustments => Set<StockAdjustment>();

    public DbSet<CycleCount> CycleCounts => Set<CycleCount>();

    public DbSet<CountBin> CountBins => Set<CountBin>();

    public DbSet<StockAlert> Alerts => Set<StockAlert>();

    public DbSet<LowStockThreshold> Thresholds => Set<LowStockThreshold>();

    public DbSet<AdjustmentReasonLookup> AdjustmentReasons => Set<AdjustmentReasonLookup>();

    async Task IWarehouseDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("WHSE");
        modelBuilder.HasOutboxTable("WHSE");
        modelBuilder.HasInboxTable("WHSE");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WhseDbContext).Assembly);
        WarehouseLookupConfiguration.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
