using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.Receiving;

namespace Romp.Modules.Quality.Infrastructure.Batches;

internal sealed class QcBatchConfiguration : IEntityTypeConfiguration<QcBatch>
{
    public void Configure(EntityTypeBuilder<QcBatch> builder)
    {
        builder.ToTable("QC_BTCH_MAIN");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(b => b.GrnId).HasColumnName("GRN_ID").IsRequired();
        builder.HasIndex(b => b.GrnId).IsUnique(); // Q8: one QC batch per goods receipt (AC-12)
        builder.Property(b => b.PoId).HasColumnName("PO_ID").IsRequired();
        builder.HasIndex(b => b.PoId);
        builder.Property(b => b.PoNo).HasColumnName("PO_NO").HasMaxLength(30).IsRequired();
        builder.Property(b => b.StatusId).HasColumnName("QC_BTCH_STS_ID").IsRequired();
        builder.HasIndex(b => b.StatusId);
        builder.Property(b => b.LotQty).HasColumnName("LOT_QTY").IsRequired();
        builder.HasAuditColumns();

        builder.HasOne<GoodsReceipt>().WithMany().HasForeignKey(b => b.GrnId)
            .HasConstraintName("FK_QC_BTCH_MAIN_GRN_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BatchStatusLookup>().WithMany().HasForeignKey(b => b.StatusId)
            .HasConstraintName("FK_QC_BTCH_MAIN_QC_BTCH_STS_ID").OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(QcBatch.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(QcBatch.SubBatches))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(QcBatch.Inspections))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class QcBatchLineConfiguration : IEntityTypeConfiguration<QcBatchLine>
{
    public void Configure(EntityTypeBuilder<QcBatchLine> builder)
    {
        builder.ToTable("QC_BTCH_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.BatchId).HasColumnName("BTCH_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(l => l.Qty).HasColumnName("QTY").IsRequired();
        builder.HasIndex(l => new { l.BatchId, l.SizeId, l.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<QcBatch>().WithMany(b => b.Lines).HasForeignKey(l => l.BatchId)
            .HasConstraintName("FK_QC_BTCH_LINE_BTCH_ID").OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QcSubBatchConfiguration : IEntityTypeConfiguration<QcSubBatch>
{
    public void Configure(EntityTypeBuilder<QcSubBatch> builder)
    {
        builder.ToTable("QC_BTCH_SPLT");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.BatchId).HasColumnName("BTCH_ID").IsRequired();
        builder.Property(s => s.SplitNo).HasColumnName("SPLT_NO").IsRequired();
        builder.HasIndex(s => new { s.BatchId, s.SplitNo }).IsUnique();
        builder.Property(s => s.ParentSubBatchId).HasColumnName("PRNT_SPLT_ID");
        builder.HasIndex(s => s.ParentSubBatchId);
        builder.Property(s => s.OutcomeId).HasColumnName("QC_OTCM_ID");
        builder.HasIndex(s => s.OutcomeId);
        builder.Property(s => s.Reason).HasColumnName("RSN").HasMaxLength(500);
        builder.Property(s => s.DecidedAt).HasColumnName("DCSN_DTE");
        builder.Property(s => s.ActingPerson).HasColumnName("ACTR_NAME").HasMaxLength(100);
        builder.Property(s => s.ReworkRoundCount).HasColumnName("RWRK_RND_CNT").IsRequired();
        builder.Property(s => s.StatusId).HasColumnName("QC_BTCH_STS_ID").IsRequired();
        builder.HasIndex(s => s.StatusId);
        builder.HasAuditColumns();

        builder.HasOne<QcBatch>().WithMany(b => b.SubBatches).HasForeignKey(s => s.BatchId)
            .HasConstraintName("FK_QC_BTCH_SPLT_BTCH_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<QcSubBatch>().WithMany().HasForeignKey(s => s.ParentSubBatchId)
            .HasConstraintName("FK_QC_BTCH_SPLT_PRNT_SPLT_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OutcomeLookup>().WithMany().HasForeignKey(s => s.OutcomeId)
            .HasConstraintName("FK_QC_BTCH_SPLT_QC_OTCM_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BatchStatusLookup>().WithMany().HasForeignKey(s => s.StatusId)
            .HasConstraintName("FK_QC_BTCH_SPLT_QC_BTCH_STS_ID").OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(QcSubBatch.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(QcSubBatch.Returns))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class QcSubBatchLineConfiguration : IEntityTypeConfiguration<QcSubBatchLine>
{
    public void Configure(EntityTypeBuilder<QcSubBatchLine> builder)
    {
        builder.ToTable("QC_BTCH_SPLT_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.SubBatchId).HasColumnName("SPLT_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(l => l.Qty).HasColumnName("QTY").IsRequired();
        builder.Property(l => l.ReturnedQty).HasColumnName("RTRN_QTY").IsRequired();
        builder.HasIndex(l => new { l.SubBatchId, l.SizeId, l.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<QcSubBatch>().WithMany(s => s.Lines).HasForeignKey(l => l.SubBatchId)
            .HasConstraintName("FK_QC_BTCH_SPLT_LINE_SPLT_ID").OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ReworkReturnConfiguration : IEntityTypeConfiguration<ReworkReturn>
{
    public void Configure(EntityTypeBuilder<ReworkReturn> builder)
    {
        builder.ToTable("QC_RWRK_RTRN");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.SubBatchId).HasColumnName("SPLT_ID").IsRequired();
        builder.HasIndex(r => r.SubBatchId);
        builder.Property(r => r.ReturnDate).HasColumnName("RTRN_DT").HasColumnType("date");
        builder.Property(r => r.ActingPerson).HasColumnName("ACTR_NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Note).HasColumnName("NOTE").HasMaxLength(500);
        builder.HasAuditColumns();

        builder.HasOne<QcSubBatch>().WithMany(s => s.Returns).HasForeignKey(r => r.SubBatchId)
            .HasConstraintName("FK_QC_RWRK_RTRN_SPLT_ID").OnDelete(DeleteBehavior.Cascade);
    }
}
