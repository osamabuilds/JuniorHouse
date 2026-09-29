using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Production.Domain;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Domain.Samples;

namespace Romp.Modules.Production.Infrastructure.Milestones;

internal sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("PROD_MLST");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(m => m.RunId).HasColumnName("RUN_ID").IsRequired();
        builder.HasIndex(m => m.RunId);
        builder.Property(m => m.TypeId).HasColumnName("MLST_TYP_ID").IsRequired();
        builder.HasIndex(m => m.TypeId);
        builder.Property(m => m.ClaimedDate).HasColumnName("CLM_DT").HasColumnType("date");
        builder.Property(m => m.ReportedBy).HasColumnName("RPTR_NAME").HasMaxLength(100).IsRequired();
        builder.Property(m => m.ActingPerson).HasColumnName("ACTR_NAME").HasMaxLength(100).IsRequired();
        builder.Property(m => m.Note).HasColumnName("NOTE").HasMaxLength(500);
        builder.Property(m => m.IsRecordedLate).HasColumnName("LATE_RCRD_IND").IsRequired();
        builder.Property(m => m.PpRoundId).HasColumnName("PP_RND_ID");
        builder.HasIndex(m => m.PpRoundId);
        builder.Property(m => m.FlagReason).HasColumnName("FLAG_RSN").HasMaxLength(500);
        builder.Ignore(m => m.IsFlagged);
        builder.HasAuditColumns();

        builder.HasOne<ProductionRun>().WithMany(r => r.Milestones).HasForeignKey(m => m.RunId)
            .HasConstraintName("FK_PROD_MLST_RUN_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MilestoneTypeLookup>().WithMany().HasForeignKey(m => m.TypeId)
            .HasConstraintName("FK_PROD_MLST_MLST_TYP_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PpSampleRound>().WithMany().HasForeignKey(m => m.PpRoundId)
            .HasConstraintName("FK_PROD_MLST_PP_RND_ID").OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(Milestone.FinishedLines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FinishedQuantityLineConfiguration : IEntityTypeConfiguration<FinishedQuantityLine>
{
    public void Configure(EntityTypeBuilder<FinishedQuantityLine> builder)
    {
        builder.ToTable("PROD_MLST_FNSH_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.MilestoneId).HasColumnName("MLST_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(l => l.Qty).HasColumnName("FNSH_QTY").IsRequired();
        builder.HasIndex(l => new { l.MilestoneId, l.SizeId, l.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<Milestone>().WithMany(m => m.FinishedLines).HasForeignKey(l => l.MilestoneId)
            .HasConstraintName("FK_PROD_MLST_FNSH_LINE_MLST_ID").OnDelete(DeleteBehavior.Cascade);
    }
}
