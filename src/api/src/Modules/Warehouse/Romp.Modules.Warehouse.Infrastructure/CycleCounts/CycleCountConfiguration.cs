using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;
using Romp.Modules.Warehouse.Domain.Bins;
using Romp.Modules.Warehouse.Domain.CycleCounts;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Infrastructure.CycleCounts;

internal sealed class CycleCountConfiguration : IEntityTypeConfiguration<CycleCount>
{
    public void Configure(EntityTypeBuilder<CycleCount> builder)
    {
        builder.ToTable("CNT_MAIN");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.CountNo).HasColumnName("CNT_NO").HasMaxLength(20).IsRequired();
        builder.Property(c => c.StatusId).HasColumnName("CNT_STS_ID").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("CRTD_BY").HasMaxLength(100).IsRequired();

        builder.HasIndex(c => c.CountNo).IsUnique();
        builder.HasIndex(c => new { c.StatusId, c.Id });
        builder.HasAuditColumns();

        builder.HasOne<CountStatusLookup>().WithMany().HasForeignKey(c => c.StatusId).HasConstraintName("FK_CNT_MAIN_CNT_STS_LKP");
        builder.Metadata.FindNavigation(nameof(CycleCount.Bins))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(CycleCount.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CountBinConfiguration : IEntityTypeConfiguration<CountBin>
{
    public void Configure(EntityTypeBuilder<CountBin> builder)
    {
        builder.ToTable("CNT_BIN");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(b => b.CountId).HasColumnName("CNT_ID").IsRequired();
        builder.Property(b => b.BinId).HasColumnName("BIN_ID").IsRequired();
        builder.Property(b => b.OpenMovementMark).HasColumnName("OPEN_MVMT_MARK").IsRequired();
        builder.Property(b => b.IsOpen).HasColumnName("OPEN_IND").IsRequired();
        builder.Property(b => b.Submitted).HasColumnName("SUBM_IND").IsRequired();

        builder.HasIndex(b => new { b.CountId, b.BinId }).IsUnique();

        // CC-3: a bin belongs to at most one OPEN count. The database decides, not only the handler's check.
        builder.HasIndex(b => b.BinId).IsUnique().HasFilter("\"OPEN_IND\" = true").HasDatabaseName("UX_CNT_BIN_OPEN");
        builder.HasIndex(b => b.BinId).HasDatabaseName("IX_CNT_BIN_BIN_ID");
        builder.HasAuditColumns();

        builder.HasOne<CycleCount>().WithMany(c => c.Bins).HasForeignKey(b => b.CountId).HasConstraintName("FK_CNT_BIN_CNT_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bin>().WithMany().HasForeignKey(b => b.BinId).HasConstraintName("FK_CNT_BIN_BIN_MAIN").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CountLineConfiguration : IEntityTypeConfiguration<CountLine>
{
    public void Configure(EntityTypeBuilder<CountLine> builder)
    {
        builder.ToTable("CNT_LINE", table => table.HasCheckConstraint("CK_CNT_LINE_CNTD_QTY_NONNEG", "\"CNTD_QTY\" >= 0"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.CountId).HasColumnName("CNT_ID").IsRequired();
        builder.Property(l => l.BinId).HasColumnName("BIN_ID").IsRequired();
        builder.Property(l => l.VariantRefId).HasColumnName("VRNT_REF_ID").IsRequired();
        builder.Property(l => l.GradeId).HasColumnName("GRD_ID").IsRequired();
        builder.Property(l => l.CountedQuantity).HasColumnName("CNTD_QTY").IsRequired();
        builder.Property(l => l.SystemQuantity).HasColumnName("SYS_QTY");
        builder.Property(l => l.ReasonId).HasColumnName("ADJ_RSN_ID");
        builder.Property(l => l.AdjustmentId).HasColumnName("ADJ_ID");

        builder.HasIndex(l => new { l.CountId, l.BinId, l.VariantRefId, l.GradeId }).IsUnique();
        builder.HasIndex(l => l.VariantRefId);
        builder.HasIndex(l => l.BinId);
        builder.HasIndex(l => l.AdjustmentId);
        builder.HasAuditColumns();

        builder.HasOne<CycleCount>().WithMany(c => c.Lines).HasForeignKey(l => l.CountId).HasConstraintName("FK_CNT_LINE_CNT_MAIN").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Bin>().WithMany().HasForeignKey(l => l.BinId).HasConstraintName("FK_CNT_LINE_BIN_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VariantRef>().WithMany().HasForeignKey(l => l.VariantRefId).HasConstraintName("FK_CNT_LINE_VRNT_REF").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GradeLookup>().WithMany().HasForeignKey(l => l.GradeId).HasConstraintName("FK_CNT_LINE_GRD_LKP");
        builder.HasOne<AdjustmentReasonLookup>().WithMany().HasForeignKey(l => l.ReasonId).HasConstraintName("FK_CNT_LINE_ADJ_RSN_LKP");
        builder.HasOne<StockAdjustment>().WithMany().HasForeignKey(l => l.AdjustmentId).HasConstraintName("FK_CNT_LINE_ADJ_MAIN").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CountNumberSequenceConfiguration : IEntityTypeConfiguration<CountNumberSequence>
{
    public void Configure(EntityTypeBuilder<CountNumberSequence> builder)
    {
        builder.ToTable("CNT_NO_SEQ");
        builder.HasKey(s => s.Year);
        builder.Property(s => s.Year).HasColumnName("YR").ValueGeneratedNever();
        builder.Property(s => s.Seq).HasColumnName("SEQ").IsRequired();
    }
}
