using Microsoft.EntityFrameworkCore;
using Romp.Modules.Production.Domain;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Domain.Samples;

namespace Romp.Modules.Production.Infrastructure.Persistence;

/// <summary>The five system-owned PROD lookups (docs/db/naming.md standard lookup shape), seeded by migration. The codes are the stable keys code refers to.</summary>
internal static class ProductionLookupConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        Configure<RunStatusLookup>(modelBuilder, "PROD_RUN_STS_LKP",
            (RunStatus.Open, "Open", "Open"), (RunStatus.Cancelled, "Cancelled", "Cancelled"), (RunStatus.Closed, "Closed", "Closed"));

        Configure<MilestoneTypeLookup>(modelBuilder, "MLST_TYP_LKP",
            (MilestoneType.FabricBooked, "FabricBooked", "Fabric booked"),
            (MilestoneType.FabricDispatchedByRomp, "FabricDispatchedByRomp", "Fabric dispatched by Romp"),
            (MilestoneType.PpSampleSubmitted, "PPSampleSubmitted", "Pre-production sample submitted"),
            (MilestoneType.PpSampleApproved, "PPSampleApproved", "Pre-production sample approved"),
            (MilestoneType.PpSampleRejected, "PPSampleRejected", "Pre-production sample rejected"),
            (MilestoneType.BulkCuttingStarted, "BulkCuttingStarted", "Bulk cutting started"),
            (MilestoneType.FinishedReadyToShip, "FinishedReadyToShip", "Finished / ready to ship"));

        Configure<PpDecisionLookup>(modelBuilder, "PP_SMPL_DCSN_LKP",
            (PpDecision.Pending, "Pending", "Pending"), (PpDecision.Approved, "Approved", "Approved"), (PpDecision.Rejected, "Rejected", "Rejected"));

        Configure<DeliveryTimingLookup>(modelBuilder, "DLVR_TMNG_LKP",
            (DeliveryTiming.OnTime, "OnTime", "On time"), (DeliveryTiming.Late, "Late", "Late"),
            (DeliveryTiming.BeyondAcceptable, "BeyondAcceptable", "Beyond the latest acceptable date"));

        Configure<DeliveryQuantityStatusLookup>(modelBuilder, "DLVR_QTY_STS_LKP",
            (DeliveryQuantityStatus.OnQuantity, "OnQuantity", "On quantity"), (DeliveryQuantityStatus.Shortage, "Short", "Short"),
            (DeliveryQuantityStatus.Over, "Over", "Over"));
    }

    private static void Configure<TLookup>(ModelBuilder modelBuilder, string tableName, params (short Id, string Code, string Name)[] rows)
        where TLookup : ProductionLookup
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

        builder.HasData(rows.Select(r => (object)new
        {
            Id = r.Id, Code = r.Code, Name = r.Name, Description = (string?)null, SortSeq = r.Id, IsActive = true,
        }).ToArray());
    }
}
