using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Fulfilment.Domain;
using Romp.Modules.Fulfilment.Domain.Allocations;
using Romp.Modules.Fulfilment.Domain.PackStation;
using Romp.Modules.Fulfilment.Domain.PickExceptions;

namespace Romp.Modules.Fulfilment.Infrastructure.Persistence;

// Every table and column is mapped explicitly (docs/db/naming.md): UPPERCASE words of 2 to 4 characters, abbreviations from the glossary.

internal sealed class FulfilmentOrderConfiguration : IEntityTypeConfiguration<FulfilmentOrder>
{
    public void Configure(EntityTypeBuilder<FulfilmentOrder> builder)
    {
        builder.ToTable("FULF_ORDR", table =>
        {
            table.HasCheckConstraint("CK_FULF_ORDR_CASH_AMT", "\"CASH_AMT\" >= 0");
        });
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(o => o.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(o => o.OrderNumber).HasColumnName("ORDR_NO").HasMaxLength(30).IsRequired();
        builder.Property(o => o.PlacementKey).HasColumnName("PLCE_KEY").HasMaxLength(64).IsRequired();
        builder.Property(o => o.StatusId).HasColumnName("FULF_STS_ID").IsRequired();
        builder.Property(o => o.IsCashOnDelivery).HasColumnName("COD_IND").IsRequired();
        builder.Property(o => o.CashToCollect).HasColumnName("CASH_AMT").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(o => o.QueuedAt).HasColumnName("QUED_DTE").IsRequired();
        builder.Property(o => o.PickClaimedBy).HasColumnName("PICK_CLM_BY").HasMaxLength(100);
        builder.Property(o => o.PickClaimedAt).HasColumnName("PICK_CLM_DTE");
        builder.Property(o => o.CurrentBin).HasColumnName("CRNT_BIN").HasMaxLength(12);
        builder.Property(o => o.PickedAt).HasColumnName("PICK_DTE");
        builder.Property(o => o.PickedBy).HasColumnName("PICK_BY").HasMaxLength(100);
        builder.Property(o => o.PackClaimedBy).HasColumnName("PACK_CLM_BY").HasMaxLength(100);
        builder.Property(o => o.PackedAt).HasColumnName("PACK_DTE");
        builder.Property(o => o.PackedBy).HasColumnName("PACK_BY").HasMaxLength(100);
        builder.Property(o => o.PutBackRequired).HasColumnName("PTBK_IND").IsRequired();
        builder.Property(o => o.SlipPrintCount).HasColumnName("SLIP_PRT_CNT").IsRequired();
        builder.Property(o => o.SlipPrintedAt).HasColumnName("SLIP_PRT_DTE");
        builder.Property(o => o.SlipPrintPending).HasColumnName("SLIP_PRT_PEND_IND").IsRequired();
        builder.Ignore(o => o.DomainEvents);
        builder.Ignore(o => o.TotalUnits);
        builder.Ignore(o => o.ActiveLines);
        builder.Ignore(o => o.IsReadyToComplete);
        builder.Ignore(o => o.IsPackable);
        builder.Ignore(o => o.CanPrintSlip);

        // One fulfilment order per order: a redelivered event is the same fulfilment order (ALC-5).
        builder.HasIndex(o => o.OrderId).IsUnique().HasDatabaseName("UQ_FULF_ORDR_ORDR_ID");
        builder.HasIndex(o => o.PlacementKey).HasDatabaseName("IX_FULF_ORDR_PLCE_KEY");
        // The queue reads one status, oldest first (NFR-SC-01, APP-8).
        builder.HasIndex(o => new { o.StatusId, o.QueuedAt }).HasDatabaseName("IX_FULF_ORDR_FULF_STS_ID_QUED_DTE");
        // The slip retry worker looks for pending prints only.
        builder.HasIndex(o => o.SlipPrintPending).HasFilter("\"SLIP_PRT_PEND_IND\"").HasDatabaseName("IX_FULF_ORDR_SLIP_PRT_PEND_IND");
        builder.HasAuditColumns();

        builder.HasOne<FulfilmentStatusLookup>().WithMany().HasForeignKey(o => o.StatusId).HasConstraintName("FK_FULF_ORDR_FULF_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId).HasConstraintName("FK_FULF_LINE_FULF_ORDR").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(o => o.Exceptions).WithOne().HasForeignKey(e => e.OrderId).HasConstraintName("FK_FULF_EXCP_FULF_ORDR").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(o => o.Pack).WithOne().HasForeignKey<PackRecord>(p => p.OrderId).HasConstraintName("FK_FULF_PACK_FULF_ORDR").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(o => o.Exceptions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FulfilmentLineConfiguration : IEntityTypeConfiguration<FulfilmentLine>
{
    public void Configure(EntityTypeBuilder<FulfilmentLine> builder)
    {
        builder.ToTable("FULF_LINE", table =>
        {
            table.HasCheckConstraint("CK_FULF_LINE_QTY", "\"QTY\" > 0");
            table.HasCheckConstraint("CK_FULF_LINE_PICK_QTY", "\"PICK_QTY\" >= 0 AND \"PICK_QTY\" <= \"QTY\"");
            table.HasCheckConstraint("CK_FULF_LINE_PACK_QTY", "\"PACK_QTY\" >= 0 AND \"PACK_QTY\" <= \"QTY\"");
        });
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(l => l.VariantId).HasColumnName("VRNT_ID").IsRequired();
        builder.Property(l => l.Sku).HasColumnName("SKU_CODE").HasMaxLength(120);
        builder.Property(l => l.Gtin).HasColumnName("GTIN").HasMaxLength(13);
        builder.Property(l => l.Name).HasColumnName("PRDT_NAME").HasMaxLength(200).IsRequired();
        builder.Property(l => l.Colour).HasColumnName("CLR_NAME").HasMaxLength(50).IsRequired();
        builder.Property(l => l.Size).HasColumnName("SIZE_NAME").HasMaxLength(30).IsRequired();
        builder.Property(l => l.Quantity).HasColumnName("QTY").IsRequired();
        builder.Property(l => l.UnitPrice).HasColumnName("UNIT_PRIC").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(l => l.StatusId).HasColumnName("LINE_STS_ID").IsRequired();
        builder.Property(l => l.PickedQuantity).HasColumnName("PICK_QTY").IsRequired();
        builder.Property(l => l.PackedQuantity).HasColumnName("PACK_QTY").IsRequired();
        builder.Ignore(l => l.IsActive);
        builder.Ignore(l => l.AllocatedQuantity);

        builder.HasIndex(l => new { l.OrderId, l.VariantId }).IsUnique().HasDatabaseName("UQ_FULF_LINE_ORDR_ID_VRNT_ID");
        // A scan finds the line by barcode within its order (PCK-6).
        builder.HasIndex(l => new { l.OrderId, l.Gtin }).HasDatabaseName("IX_FULF_LINE_ORDR_ID_GTIN");
        builder.HasIndex(l => l.StatusId).HasDatabaseName("IX_FULF_LINE_LINE_STS_ID");
        builder.HasAuditColumns();

        builder.HasOne<LineStatusLookup>().WithMany().HasForeignKey(l => l.StatusId).HasConstraintName("FK_FULF_LINE_LINE_STS_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(l => l.Allocations).WithOne().HasForeignKey(a => a.LineId).HasConstraintName("FK_FULF_ALOC_FULF_LINE").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LineAllocationConfiguration : IEntityTypeConfiguration<LineAllocation>
{
    public void Configure(EntityTypeBuilder<LineAllocation> builder)
    {
        builder.ToTable("FULF_ALOC", table =>
        {
            table.HasCheckConstraint("CK_FULF_ALOC_QTY", "\"QTY\" >= 0");
            table.HasCheckConstraint("CK_FULF_ALOC_PICK_QTY", "\"PICK_QTY\" >= 0 AND \"PICK_QTY\" <= \"QTY\"");
        });
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.LineId).HasColumnName("LINE_ID").IsRequired();
        builder.Property(a => a.BinId).HasColumnName("BIN_ID").IsRequired();
        builder.Property(a => a.BinCode).HasColumnName("BIN_CODE").HasMaxLength(12).IsRequired();
        builder.Property(a => a.WalkKey).HasColumnName("WALK_KEY").HasMaxLength(12).IsRequired();
        builder.Property(a => a.Quantity).HasColumnName("QTY").IsRequired();
        builder.Property(a => a.PickedQuantity).HasColumnName("PICK_QTY").IsRequired();
        builder.Ignore(a => a.Remaining);

        builder.HasIndex(a => new { a.LineId, a.BinId }).IsUnique().HasDatabaseName("UQ_FULF_ALOC_LINE_ID_BIN_ID");
        builder.HasIndex(a => new { a.LineId, a.BinCode }).HasDatabaseName("IX_FULF_ALOC_LINE_ID_BIN_CODE");
        builder.HasAuditColumns();
    }
}

internal sealed class PickExceptionConfiguration : IEntityTypeConfiguration<PickExceptionRecord>
{
    public void Configure(EntityTypeBuilder<PickExceptionRecord> builder)
    {
        builder.ToTable("FULF_EXCP", table =>
        {
            table.HasCheckConstraint("CK_FULF_EXCP_QTY", "\"SYS_QTY\" >= 0 AND \"FND_QTY\" >= 0");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(e => e.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(e => e.VariantId).HasColumnName("VRNT_ID").IsRequired();
        builder.Property(e => e.BinId).HasColumnName("BIN_ID").IsRequired();
        builder.Property(e => e.BinCode).HasColumnName("BIN_CODE").HasMaxLength(12).IsRequired();
        builder.Property(e => e.ReasonId).HasColumnName("EXCP_RSN_ID").IsRequired();
        builder.Property(e => e.StatusId).HasColumnName("EXCP_STS_ID").IsRequired();
        builder.Property(e => e.SystemQuantity).HasColumnName("SYS_QTY").IsRequired();
        builder.Property(e => e.FoundQuantity).HasColumnName("FND_QTY").IsRequired();
        builder.Property(e => e.RaisedBy).HasColumnName("RAIS_BY").HasMaxLength(100).IsRequired();
        builder.Property(e => e.RaisedAt).HasColumnName("RAIS_DTE").IsRequired();
        builder.Property(e => e.ResolvedBy).HasColumnName("RSLV_BY").HasMaxLength(100);
        builder.Property(e => e.ResolvedAt).HasColumnName("RSLV_DTE");
        builder.Property(e => e.WasRestocked).HasColumnName("RSTK_IND");

        // The staff queue reads open exceptions, oldest first (PEX-4).
        builder.HasIndex(e => new { e.StatusId, e.RaisedAt }).HasDatabaseName("IX_FULF_EXCP_EXCP_STS_ID_RAIS_DTE");
        builder.HasIndex(e => e.OrderId).HasDatabaseName("IX_FULF_EXCP_ORDR_ID");
        builder.HasIndex(e => e.ReasonId).HasDatabaseName("IX_FULF_EXCP_EXCP_RSN_ID");
        builder.HasAuditColumns();

        builder.HasOne<ExceptionReasonLookup>().WithMany().HasForeignKey(e => e.ReasonId).HasConstraintName("FK_FULF_EXCP_EXCP_RSN_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExceptionStatusLookup>().WithMany().HasForeignKey(e => e.StatusId).HasConstraintName("FK_FULF_EXCP_EXCP_STS_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PackRecordConfiguration : IEntityTypeConfiguration<PackRecord>
{
    public void Configure(EntityTypeBuilder<PackRecord> builder)
    {
        builder.ToTable("FULF_PACK", table =>
        {
            table.HasCheckConstraint("CK_FULF_PACK_WGHT_GRM", "\"WGHT_GRM\" > 0 AND \"WGHT_GRM\" <= 30000");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(p => p.WeightGrams).HasColumnName("WGHT_GRM").IsRequired();
        builder.Property(p => p.ExpectedGrams).HasColumnName("EXPC_GRM").IsRequired();
        builder.Property(p => p.PackagingKindId).HasColumnName("PACK_KIND_ID").IsRequired();
        builder.Property(p => p.IsDeviation).HasColumnName("DVTN_IND").IsRequired();
        builder.Property(p => p.Note).HasColumnName("NOTE").HasMaxLength(PackRecord.MaxNoteLength);
        builder.Property(p => p.WeighedBy).HasColumnName("WGHT_BY").HasMaxLength(100).IsRequired();
        builder.Property(p => p.WeighedAt).HasColumnName("WGHT_DTE").IsRequired();

        builder.HasIndex(p => p.OrderId).IsUnique().HasDatabaseName("UQ_FULF_PACK_ORDR_ID");
        builder.HasIndex(p => p.PackagingKindId).HasDatabaseName("IX_FULF_PACK_PACK_KIND_ID");
        builder.HasAuditColumns();

        builder.HasOne<PackagingKindLookup>().WithMany().HasForeignKey(p => p.PackagingKindId).HasConstraintName("FK_FULF_PACK_PACK_KIND_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>The compact scan log: not audited (it would double every scan), purged after 90 days; the partition key when volume needs it is the month of the scan.</summary>
internal sealed class ScanEntryConfiguration : IEntityTypeConfiguration<ScanEntry>
{
    public void Configure(EntityTypeBuilder<ScanEntry> builder)
    {
        builder.ToTable("FULF_SCAN");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.OrderId).HasColumnName("ORDR_ID").IsRequired();
        builder.Property(s => s.StageId).HasColumnName("SCAN_STG_ID").IsRequired();
        builder.Property(s => s.Code).HasColumnName("SCAN_CODE").HasMaxLength(40).IsRequired();
        builder.Property(s => s.OutcomeId).HasColumnName("SCAN_OTCM_ID").IsRequired();
        builder.Property(s => s.ResultCode).HasColumnName("RSLT_CODE").HasMaxLength(30).IsRequired();
        builder.Property(s => s.Staff).HasColumnName("ACTR_NAME").HasMaxLength(100).IsRequired();
        builder.Property(s => s.At).HasColumnName("SCAN_DTE").IsRequired();

        builder.HasIndex(s => s.At).HasDatabaseName("IX_FULF_SCAN_SCAN_DTE");
        builder.HasIndex(s => s.OrderId).HasDatabaseName("IX_FULF_SCAN_ORDR_ID");
        builder.HasIndex(s => s.StageId).HasDatabaseName("IX_FULF_SCAN_SCAN_STG_ID");
        builder.HasIndex(s => s.OutcomeId).HasDatabaseName("IX_FULF_SCAN_SCAN_OTCM_ID");

        builder.HasOne<ScanStageLookup>().WithMany().HasForeignKey(s => s.StageId).HasConstraintName("FK_FULF_SCAN_SCAN_STG_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScanOutcomeLookup>().WithMany().HasForeignKey(s => s.OutcomeId).HasConstraintName("FK_FULF_SCAN_SCAN_OTCM_LKP").OnDelete(DeleteBehavior.Restrict);
    }
}
