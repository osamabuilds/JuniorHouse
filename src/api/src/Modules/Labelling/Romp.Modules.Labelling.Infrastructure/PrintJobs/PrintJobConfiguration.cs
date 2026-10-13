using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.LabelWork;
using Romp.Modules.Labelling.Domain.PrintJobs;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Infrastructure.PrintJobs;

internal sealed class PrintJobConfiguration : IEntityTypeConfiguration<PrintJob>
{
    public void Configure(EntityTypeBuilder<PrintJob> builder)
    {
        builder.ToTable("PRT_JOB");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(j => j.JobNo).HasColumnName("JOB_NO").HasMaxLength(30).IsRequired();
        builder.HasIndex(j => j.JobNo).IsUnique();
        builder.Property(j => j.KindId).HasColumnName("PRT_JOB_KIND_ID").IsRequired();
        builder.Property(j => j.VariantGtinId).HasColumnName("VRNT_GTIN_ID").IsRequired();
        builder.Property(j => j.WorkItemId).HasColumnName("WORK_ITEM_ID");
        builder.Property(j => j.Qty).HasColumnName("QTY").IsRequired();
        builder.Property(j => j.OutcomeId).HasColumnName("LBL_OUTC_ID").IsRequired();
        builder.Property(j => j.PriceSnapshot).HasColumnName("PRIC_SNAP").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(j => j.PrinterId).HasColumnName("PRTR_ID").IsRequired();
        builder.Property(j => j.StatusId).HasColumnName("PRT_JOB_STS_ID").IsRequired();
        builder.Property(j => j.AttemptCount).HasColumnName("ATMP_CNT").IsRequired();
        builder.Property(j => j.NextAttemptAt).HasColumnName("NXT_ATMP_DTE").IsRequired();
        builder.Property(j => j.LastError).HasColumnName("LAST_ERR").HasMaxLength(PrintJob.MaxErrorLength);
        builder.Property(j => j.ReprintReasonId).HasColumnName("RPRT_RSN_ID");
        builder.Property(j => j.Note).HasColumnName("NOTE").HasMaxLength(PrintJob.MaxNoteLength);
        builder.Property(j => j.RequestedBy).HasColumnName("REQ_BY").HasMaxLength(100).IsRequired();
        builder.Property(j => j.ManualActionBy).HasColumnName("MNL_ACT_BY").HasMaxLength(100);

        // The worker's claim query: due Queued jobs of one printer, oldest first.
        builder.HasIndex(j => new { j.PrinterId, j.StatusId, j.NextAttemptAt }).HasDatabaseName("IX_PRT_JOB_CLAIM");
        builder.HasIndex(j => j.StatusId);
        builder.HasIndex(j => j.VariantGtinId);
        builder.HasIndex(j => j.WorkItemId);
        builder.HasAuditColumns();

        builder.HasMany(j => j.Attempts).WithOne().HasForeignKey(a => a.PrintJobId)
            .HasConstraintName("FK_PRT_JOB_ATMP_PRT_JOB").OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(PrintJob.Attempts))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<VariantGtin>().WithMany().HasForeignKey(j => j.VariantGtinId).HasConstraintName("FK_PRT_JOB_VRNT_GTIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LabelWorkItem>().WithMany().HasForeignKey(j => j.WorkItemId).HasConstraintName("FK_PRT_JOB_LBL_WORK_ITEM").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PrintJobKindLookup>().WithMany().HasForeignKey(j => j.KindId).HasConstraintName("FK_PRT_JOB_PRT_JOB_KIND_LKP");
        builder.HasOne<PrintJobStatusLookup>().WithMany().HasForeignKey(j => j.StatusId).HasConstraintName("FK_PRT_JOB_PRT_JOB_STS_LKP");
        builder.HasOne<LabelOutcomeLookup>().WithMany().HasForeignKey(j => j.OutcomeId).HasConstraintName("FK_PRT_JOB_LBL_OUTC_LKP");
        builder.HasOne<PrinterLookup>().WithMany().HasForeignKey(j => j.PrinterId).HasConstraintName("FK_PRT_JOB_PRTR_LKP");
        builder.HasOne<ReprintReasonLookup>().WithMany().HasForeignKey(j => j.ReprintReasonId).HasConstraintName("FK_PRT_JOB_RPRT_RSN_LKP");
    }
}

internal sealed class PrintAttemptConfiguration : IEntityTypeConfiguration<PrintAttempt>
{
    public void Configure(EntityTypeBuilder<PrintAttempt> builder)
    {
        builder.ToTable("PRT_JOB_ATMP");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.PrintJobId).HasColumnName("PRT_JOB_ID").IsRequired();
        builder.Property(a => a.AttemptNo).HasColumnName("ATMP_NO").IsRequired();
        builder.Property(a => a.OutcomeId).HasColumnName("PRT_ATMP_OTCM_ID").IsRequired();
        builder.Property(a => a.Error).HasColumnName("ERR_TEXT").HasMaxLength(PrintJob.MaxErrorLength);
        builder.Property(a => a.OutputReference).HasColumnName("OUTP_REF").HasMaxLength(200);
        builder.Property(a => a.At).HasColumnName("ATMP_DTE").IsRequired();
        builder.HasIndex(a => a.PrintJobId);
        builder.HasAuditColumns();

        builder.HasOne<AttemptOutcomeLookup>().WithMany().HasForeignKey(a => a.OutcomeId).HasConstraintName("FK_PRT_JOB_ATMP_PRT_ATMP_OTCM_LKP");
    }
}

internal sealed class JobNumberSequenceConfiguration : IEntityTypeConfiguration<JobNumberSequence>
{
    public void Configure(EntityTypeBuilder<JobNumberSequence> builder)
    {
        builder.ToTable("JOB_NO_SEQ");
        builder.HasKey(s => s.Year);
        builder.Property(s => s.Year).HasColumnName("YR").ValueGeneratedNever();
        builder.Property(s => s.Seq).HasColumnName("SEQ").IsRequired();
    }
}
