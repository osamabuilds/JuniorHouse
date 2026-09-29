using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;

namespace Romp.Modules.Quality.Infrastructure.Inspections;

internal sealed class InspectionConfiguration : IEntityTypeConfiguration<Inspection>
{
    public void Configure(EntityTypeBuilder<Inspection> builder)
    {
        builder.ToTable("QC_INSP_MAIN");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(i => i.BatchId).HasColumnName("BTCH_ID").IsRequired();
        builder.Property(i => i.SubBatchId).HasColumnName("SPLT_ID");
        builder.Property(i => i.RoundNo).HasColumnName("RND_NO").IsRequired();

        // A whole-batch inspection has no sub-batch (null); nulls compare equal so the round is still unique per batch.
        builder.HasIndex(i => new { i.BatchId, i.SubBatchId, i.RoundNo }).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(i => i.SubBatchId);
        builder.Property(i => i.ModeId).HasColumnName("QC_INSP_MODE_ID").IsRequired();
        builder.HasIndex(i => i.ModeId);
        builder.Property(i => i.InspectionLevelId).HasColumnName("QC_INSP_LVL_ID").IsRequired();
        builder.HasIndex(i => i.InspectionLevelId);
        builder.Property(i => i.AqlLevelId).HasColumnName("QC_AQL_LVL_ID").IsRequired();
        builder.HasIndex(i => i.AqlLevelId);
        builder.Property(i => i.LotQty).HasColumnName("LOT_QTY").IsRequired();
        builder.Property(i => i.SampleQty).HasColumnName("SMPL_QTY").IsRequired();
        builder.Property(i => i.AcceptNo).HasColumnName("ACPT_NO").IsRequired();
        builder.Property(i => i.RejectNo).HasColumnName("RJCT_NO").IsRequired();
        builder.Property(i => i.ResultId).HasColumnName("QC_INSP_RSLT_ID").IsRequired();
        builder.HasIndex(i => i.ResultId);
        builder.Property(i => i.InspectionDate).HasColumnName("INSP_DT").HasColumnType("date");
        builder.Property(i => i.ActingPerson).HasColumnName("ACTR_NAME").HasMaxLength(100).IsRequired();
        builder.HasAuditColumns();

        builder.HasOne<QcBatch>().WithMany(b => b.Inspections).HasForeignKey(i => i.BatchId)
            .HasConstraintName("FK_QC_INSP_MAIN_BTCH_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<QcSubBatch>().WithMany().HasForeignKey(i => i.SubBatchId)
            .HasConstraintName("FK_QC_INSP_MAIN_SPLT_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionModeLookup>().WithMany().HasForeignKey(i => i.ModeId)
            .HasConstraintName("FK_QC_INSP_MAIN_QC_INSP_MODE_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionLevelLookup>().WithMany().HasForeignKey(i => i.InspectionLevelId)
            .HasConstraintName("FK_QC_INSP_MAIN_QC_INSP_LVL_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AqlLevelLookup>().WithMany().HasForeignKey(i => i.AqlLevelId)
            .HasConstraintName("FK_QC_INSP_MAIN_QC_AQL_LVL_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionResultLookup>().WithMany().HasForeignKey(i => i.ResultId)
            .HasConstraintName("FK_QC_INSP_MAIN_QC_INSP_RSLT_ID").OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(Inspection.Checks))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Inspection.Defects))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Inspection.SafetyChecks))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class InspectionCheckConfiguration : IEntityTypeConfiguration<InspectionCheck>
{
    public void Configure(EntityTypeBuilder<InspectionCheck> builder)
    {
        builder.ToTable("QC_INSP_CHK");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.InspectionId).HasColumnName("INSP_ID").IsRequired();
        builder.Property(c => c.CheckTypeId).HasColumnName("QC_CHK_TYP_ID").IsRequired();
        builder.Property(c => c.ResultId).HasColumnName("QC_CHK_RSLT_ID").IsRequired();
        builder.HasIndex(c => new { c.InspectionId, c.CheckTypeId }).IsUnique();
        builder.HasIndex(c => c.CheckTypeId);
        builder.HasIndex(c => c.ResultId);
        builder.HasAuditColumns();

        builder.HasOne<Inspection>().WithMany(i => i.Checks).HasForeignKey(c => c.InspectionId)
            .HasConstraintName("FK_QC_INSP_CHK_INSP_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CheckTypeLookup>().WithMany().HasForeignKey(c => c.CheckTypeId)
            .HasConstraintName("FK_QC_INSP_CHK_QC_CHK_TYP_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CheckResultLookup>().WithMany().HasForeignKey(c => c.ResultId)
            .HasConstraintName("FK_QC_INSP_CHK_QC_CHK_RSLT_ID").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DefectConfiguration : IEntityTypeConfiguration<Defect>
{
    public void Configure(EntityTypeBuilder<Defect> builder)
    {
        builder.ToTable("QC_DFCT");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(d => d.InspectionId).HasColumnName("INSP_ID").IsRequired();
        builder.HasIndex(d => d.InspectionId);
        builder.Property(d => d.DefectTypeId).HasColumnName("QC_DFCT_TYP_ID").IsRequired();
        builder.HasIndex(d => d.DefectTypeId);
        builder.Property(d => d.SeverityId).HasColumnName("QC_DFCT_SVRT_ID").IsRequired();
        builder.HasIndex(d => d.SeverityId);
        builder.Property(d => d.Qty).HasColumnName("QTY").IsRequired();
        builder.Property(d => d.SizeId).HasColumnName("SIZE_ID");
        builder.Property(d => d.ColourId).HasColumnName("CLR_ID");
        builder.Property(d => d.Note).HasColumnName("NOTE").HasMaxLength(500).IsRequired();
        builder.HasAuditColumns();

        builder.HasOne<Inspection>().WithMany(i => i.Defects).HasForeignKey(d => d.InspectionId)
            .HasConstraintName("FK_QC_DFCT_INSP_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DefectTypeLookup>().WithMany().HasForeignKey(d => d.DefectTypeId)
            .HasConstraintName("FK_QC_DFCT_QC_DFCT_TYP_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DefectSeverityLookup>().WithMany().HasForeignKey(d => d.SeverityId)
            .HasConstraintName("FK_QC_DFCT_QC_DFCT_SVRT_ID").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AqlSampleRowConfiguration : IEntityTypeConfiguration<AqlSampleRow>
{
    public void Configure(EntityTypeBuilder<AqlSampleRow> builder)
    {
        builder.ToTable("QC_AQL_SMPL");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(r => r.InspectionLevelId).HasColumnName("QC_INSP_LVL_ID").IsRequired();
        builder.Property(r => r.LotMinQty).HasColumnName("LOT_MIN_QTY").IsRequired();
        builder.Property(r => r.LotMaxQty).HasColumnName("LOT_MAX_QTY").IsRequired();
        builder.HasIndex(r => new { r.InspectionLevelId, r.LotMinQty }).IsUnique();
        builder.Property(r => r.CodeLetter).HasColumnName("CODE_LTR").HasMaxLength(2).IsRequired();
        builder.Property(r => r.SampleQty).HasColumnName("SMPL_QTY").IsRequired();

        builder.HasOne<InspectionLevelLookup>().WithMany().HasForeignKey(r => r.InspectionLevelId)
            .HasConstraintName("FK_QC_AQL_SMPL_QC_INSP_LVL_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasData(AqlSeedData.SampleRows().Select(r => (object)new
        {
            r.Id, r.InspectionLevelId, r.LotMinQty, r.LotMaxQty, r.CodeLetter, r.SampleQty,
        }).ToArray());
    }
}

internal sealed class AqlLimitRowConfiguration : IEntityTypeConfiguration<AqlLimitRow>
{
    public void Configure(EntityTypeBuilder<AqlLimitRow> builder)
    {
        builder.ToTable("QC_AQL_LIMT");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(r => r.CodeLetter).HasColumnName("CODE_LTR").HasMaxLength(2).IsRequired();
        builder.Property(r => r.AqlLevelId).HasColumnName("QC_AQL_LVL_ID").IsRequired();
        builder.HasIndex(r => new { r.AqlLevelId, r.CodeLetter }).IsUnique();
        builder.Property(r => r.AcceptNo).HasColumnName("ACPT_NO").IsRequired();
        builder.Property(r => r.RejectNo).HasColumnName("RJCT_NO").IsRequired();

        builder.HasOne<AqlLevelLookup>().WithMany().HasForeignKey(r => r.AqlLevelId)
            .HasConstraintName("FK_QC_AQL_LIMT_QC_AQL_LVL_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasData(AqlSeedData.LimitRows().Select(r => (object)new
        {
            r.Id, r.CodeLetter, r.AqlLevelId, r.AcceptNo, r.RejectNo,
        }).ToArray());
    }
}
