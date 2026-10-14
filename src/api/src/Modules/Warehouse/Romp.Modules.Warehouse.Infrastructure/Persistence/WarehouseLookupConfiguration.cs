using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;
using Romp.Modules.Warehouse.Domain.CycleCounts;
using Romp.Modules.Warehouse.Domain.Putaway;
using Romp.Modules.Warehouse.Domain.StockAlerts;

namespace Romp.Modules.Warehouse.Infrastructure.Persistence;

/// <summary>
/// The system-owned Warehouse lookups (docs/db/naming.md standard lookup shape), seeded by migration. The codes are the stable keys
/// that code refers to. Features add their own lookups here as they land.
/// </summary>
internal static class WarehouseLookupConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        Configure<GradeLookup>(modelBuilder, "GRD_LKP",
            (Grades.Accepted, "ACCEPTED", "Accepted", "Accepted by QC"),
            (Grades.Concession, "CONCESSION", "Concession", "Accepted with concession (2ND mark)"));

        Configure<MovementKindLookup>(modelBuilder, "MVMT_KIND_LKP",
            (MovementKinds.Putaway, "PUTAWAY", "Putaway", "Stock shelved by scan"),
            (MovementKinds.Adjustment, "ADJUSTMENT", "Adjustment", "Reason-coded correction"));

        Configure<PutawayStatusLookup>(modelBuilder, "PUT_STS_LKP",
            (PutawayStatuses.AwaitingGtin, "AWAITING_GTIN", "Awaiting GTIN", "Accepted by QC; Labelling has not assigned the barcode yet"),
            (PutawayStatuses.Pending, "PENDING", "Pending", "Ready to be put away"),
            (PutawayStatuses.Complete, "COMPLETE", "Complete", "All units are on the shelf"));

        Configure<AdjustmentReasonLookup>(modelBuilder, "ADJ_RSN_LKP",
            (AdjustmentReasons.Damage, "DAMAGE", "Damage", "Units damaged on the shelf or in handling"),
            (AdjustmentReasons.Miscount, "MISCOUNT", "Miscount", "The system count was wrong"),
            (AdjustmentReasons.Theft, "THEFT", "Theft", "Units are missing and believed stolen"),
            (AdjustmentReasons.FoundStock, "FOUND_STOCK", "Found stock", "Units found that the system did not know about"));

        Configure<CountStatusLookup>(modelBuilder, "CNT_STS_LKP",
            (CountStatuses.Open, "OPEN", "Open", "Being counted"),
            (CountStatuses.Closed, "CLOSED", "Closed", "Confirmed; variances adjusted"),
            (CountStatuses.Cancelled, "CANCELLED", "Cancelled", "Cancelled; no stock changed"));

        Configure<AlertLevelLookup>(modelBuilder, "ALRT_LVL_LKP",
            (AlertLevels.Low, "LOW", "Low stock", "Available is at or below the threshold"),
            (AlertLevels.Out, "OUT", "Stock-out", "Nothing available"));

        Configure<AlertStatusLookup>(modelBuilder, "ALRT_STS_LKP",
            (AlertStatuses.Open, "OPEN", "Open", "Stock is still low"),
            (AlertStatuses.Closed, "CLOSED", "Closed", "Stock recovered above the threshold"));
    }

    public static void Configure<TLookup>(ModelBuilder modelBuilder, string tableName, params (short Id, string Code, string Name, string Description)[] rows)
        where TLookup : WarehouseLookup
    {
        var builder = ConfigureShape<TLookup>(modelBuilder, tableName);
        builder.HasData(rows.Select(r => (object)new
        {
            Id = r.Id, Code = r.Code, Name = r.Name, Description = (string?)r.Description, SortSeq = r.Id, IsActive = true,
        }).ToArray());
    }

    private static EntityTypeBuilder<TLookup> ConfigureShape<TLookup>(ModelBuilder modelBuilder, string tableName)
        where TLookup : WarehouseLookup
    {
        var builder = modelBuilder.Entity<TLookup>();
        builder.ToTable(tableName);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(l => l.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(l => l.IsActive).HasColumnName("ACT_IND").IsRequired();
        return builder;
    }
}
