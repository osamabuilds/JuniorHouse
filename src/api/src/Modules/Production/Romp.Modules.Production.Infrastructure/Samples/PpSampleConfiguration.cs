using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Production.Domain;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Domain.Samples;

namespace Romp.Modules.Production.Infrastructure.Samples;

internal sealed class PpSampleRoundConfiguration : IEntityTypeConfiguration<PpSampleRound>
{
    public void Configure(EntityTypeBuilder<PpSampleRound> builder)
    {
        builder.ToTable("PP_SMPL_RND");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.RunId).HasColumnName("RUN_ID").IsRequired();
        builder.Property(r => r.RoundNo).HasColumnName("RND_NO").IsRequired();
        builder.HasIndex(r => new { r.RunId, r.RoundNo }).IsUnique();
        builder.Property(r => r.SubmittedDate).HasColumnName("SUBM_DT").HasColumnType("date");
        builder.Property(r => r.SubmittedBy).HasColumnName("SUBM_NAME").HasMaxLength(100).IsRequired();
        builder.Property(r => r.Note).HasColumnName("NOTE").HasMaxLength(500);
        builder.Property(r => r.DecisionId).HasColumnName("PP_SMPL_DCSN_ID").IsRequired();
        builder.HasIndex(r => r.DecisionId);
        builder.Property(r => r.DecidedAt).HasColumnName("DCSN_DTE");
        builder.Property(r => r.DecidedBy).HasColumnName("DCSN_NAME").HasMaxLength(100);
        builder.Property(r => r.DecisionReason).HasColumnName("DCSN_RSN").HasMaxLength(500);
        builder.Ignore(r => r.IsPending);
        builder.HasAuditColumns();

        builder.HasOne<ProductionRun>().WithMany(p => p.PpRounds).HasForeignKey(r => r.RunId)
            .HasConstraintName("FK_PP_SMPL_RND_RUN_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PpDecisionLookup>().WithMany().HasForeignKey(r => r.DecisionId)
            .HasConstraintName("FK_PP_SMPL_RND_PP_SMPL_DCSN_ID").OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(PpSampleRound.Files))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PpSampleFileConfiguration : IEntityTypeConfiguration<PpSampleFile>
{
    public void Configure(EntityTypeBuilder<PpSampleFile> builder)
    {
        builder.ToTable("PP_SMPL_FILE");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(f => f.RoundId).HasColumnName("RND_ID").IsRequired();
        builder.HasIndex(f => f.RoundId);
        builder.Property(f => f.StorageKey).HasColumnName("STOR_KEY").HasMaxLength(100).IsRequired();
        builder.Property(f => f.FileName).HasColumnName("FILE_NAME").HasMaxLength(255).IsRequired();
        builder.Property(f => f.ContentType).HasColumnName("CNTT_TYP").HasMaxLength(100).IsRequired();
        builder.Property(f => f.FileSizeBytes).HasColumnName("FILE_SIZE_BYT").IsRequired();
        builder.Property(f => f.IsDeleted).HasColumnName("DELD_IND").IsRequired();
        builder.HasAuditColumns();

        builder.HasOne<PpSampleRound>().WithMany(r => r.Files).HasForeignKey(f => f.RoundId)
            .HasConstraintName("FK_PP_SMPL_FILE_RND_ID").OnDelete(DeleteBehavior.Cascade);
    }
}
