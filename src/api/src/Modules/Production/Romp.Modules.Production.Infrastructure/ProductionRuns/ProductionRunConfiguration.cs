using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Production.Domain;
using Romp.Modules.Production.Domain.ProductionRuns;

namespace Romp.Modules.Production.Infrastructure.ProductionRuns;

internal sealed class ProductionRunConfiguration : IEntityTypeConfiguration<ProductionRun>
{
    public void Configure(EntityTypeBuilder<ProductionRun> builder)
    {
        builder.ToTable("PROD_RUN_MAIN");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.PoId).HasColumnName("PO_ID").IsRequired();
        builder.HasIndex(r => r.PoId).IsUnique(); // AC-2: one run per PO, even under a duplicate delivery
        builder.Property(r => r.PoNo).HasColumnName("PO_NO").HasMaxLength(15).IsRequired();
        builder.Property(r => r.FabricResponsibilityId).HasColumnName("FBRC_RESP_ID");
        builder.Property(r => r.PoSentDate).HasColumnName("PO_SENT_DT").HasColumnType("date");
        builder.Property(r => r.LatestAcceptableDate).HasColumnName("LATE_ACPT_DT").HasColumnType("date");
        builder.Property(r => r.StatusId).HasColumnName("PROD_RUN_STS_ID").IsRequired();
        builder.HasIndex(r => r.StatusId);
        builder.Property(r => r.ExpectedCompletionDate).HasColumnName("EXPC_CMPL_DT").HasColumnType("date");
        builder.HasIndex(r => r.ExpectedCompletionDate);
        builder.Property(r => r.CuttingStartedDate).HasColumnName("CUT_STRT_DT").HasColumnType("date");
        builder.Property(r => r.FabricDispatchDate).HasColumnName("FBRC_DSPT_DT").HasColumnType("date");
        builder.Property(r => r.IsCuttingBeforeFabricDispatch).HasColumnName("CUT_BEF_FBRC_IND").IsRequired();
        builder.Property(r => r.IsResampleRecommended).HasColumnName("RSMP_RECM_IND").IsRequired();
        builder.Property(r => r.IsCuttingWithoutResample).HasColumnName("CUT_WOUT_RSMP_IND").IsRequired();
        builder.Property(r => r.IsApprovalSuperseded).HasColumnName("APRV_SUPR_IND").IsRequired();
        builder.Ignore(r => r.IsOpen);
        builder.Ignore(r => r.HasApprovedPpSample);
        builder.Ignore(r => r.IsCuttingStarted);
        builder.HasAuditColumns();

        builder.HasOne<RunStatusLookup>().WithMany().HasForeignKey(r => r.StatusId)
            .HasConstraintName("FK_PROD_RUN_MAIN_PROD_RUN_STS_ID").OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(ProductionRun.Milestones))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(ProductionRun.PpRounds))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(ProductionRun.ExpectedHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(ProductionRun.DeliveryNotes))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ExpectedCompletionChangeConfiguration : IEntityTypeConfiguration<ExpectedCompletionChange>
{
    public void Configure(EntityTypeBuilder<ExpectedCompletionChange> builder)
    {
        builder.ToTable("PROD_EXPC_HIST");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.RunId).HasColumnName("RUN_ID").IsRequired();
        builder.HasIndex(c => c.RunId);
        builder.Property(c => c.OldDate).HasColumnName("OLD_DT").HasColumnType("date");
        builder.Property(c => c.NewDate).HasColumnName("NEW_DT").HasColumnType("date").IsRequired();
        builder.Property(c => c.Reason).HasColumnName("RSN").HasMaxLength(500);
        builder.Property(c => c.ActingPerson).HasColumnName("ACTR_NAME").HasMaxLength(100).IsRequired();
        builder.HasAuditColumns();

        builder.HasOne<ProductionRun>().WithMany(r => r.ExpectedHistory).HasForeignKey(c => c.RunId)
            .HasConstraintName("FK_PROD_EXPC_HIST_RUN_ID").OnDelete(DeleteBehavior.Cascade);
    }
}
