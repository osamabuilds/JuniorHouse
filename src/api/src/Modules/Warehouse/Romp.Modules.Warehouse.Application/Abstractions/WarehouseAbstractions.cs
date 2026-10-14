using Microsoft.EntityFrameworkCore;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;
using Romp.Modules.Warehouse.Domain.Bins;
using Romp.Modules.Warehouse.Domain.CycleCounts;
using Romp.Modules.Warehouse.Domain.Putaway;
using Romp.Modules.Warehouse.Domain.StockAlerts;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Application.Abstractions;

/// <summary>Marks a MediatR command as belonging to Warehouse so its transaction behaviour (which alone knows the DbContext) applies to it.</summary>
public interface IWarehouseCommand;

/// <summary>Warehouse's persistence port (Application owns it, Infrastructure's WhseDbContext implements it; ADR 0002). Feature sets are added with their features.</summary>
public interface IWarehouseDbContext
{
    DbSet<Bin> Bins { get; }

    DbSet<VariantRef> VariantRefs { get; }

    DbSet<StockBalance> Balances { get; }

    DbSet<StockMovement> Movements { get; }

    DbSet<PutawayItem> PutawayItems { get; }

    DbSet<PutawayRecord> PutawayRecords { get; }

    DbSet<StockAdjustment> Adjustments { get; }

    DbSet<CycleCount> CycleCounts { get; }

    DbSet<CountBin> CountBins { get; }

    DbSet<StockAlert> Alerts { get; }

    DbSet<LowStockThreshold> Thresholds { get; }

    DbSet<AdjustmentReasonLookup> AdjustmentReasons { get; }

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Configuration, not constants (D5, D13). Bound from <c>Warehouse:*</c> and validated at start-up by the module.
/// </summary>
public sealed class WarehouseOptions
{
    /// <summary>An adjustment above this many units needs an explicit re-confirmation (ADJ-4b, D5). Default 20.</summary>
    public int LargeAdjustmentUnits { get; set; } = 20;

    /// <summary>Available quantity at or below which a variant raises a low-stock alert (ALR-1, D13). Default 5; 0 means only stock-out.</summary>
    public int DefaultLowStockThreshold { get; set; } = 5;

    /// <summary>A QC message with more lines than this is refused as malformed (PUT-1, resource limit).</summary>
    public int MaxLinesPerAcceptedBatch { get; set; } = 500;
}
