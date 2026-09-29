using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.SafetyChecks;

namespace Romp.Modules.Quality.Infrastructure.SafetyChecks;

internal sealed class SafetyCheckConfiguration : IEntityTypeConfiguration<SafetyCheck>
{
    public void Configure(EntityTypeBuilder<SafetyCheck> builder)
    {
        builder.ToTable("QC_INSP_SAFE");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.InspectionId).HasColumnName("INSP_ID").IsRequired();
        builder.Property(s => s.SafetyTypeId).HasColumnName("QC_SAFE_TYP_ID").IsRequired();
        builder.Property(s => s.ResultId).HasColumnName("QC_CHK_RSLT_ID").IsRequired();
        builder.Property(s => s.Reason).HasColumnName("RSN").HasMaxLength(500);
        builder.HasIndex(s => new { s.InspectionId, s.SafetyTypeId }).IsUnique(); // AC-31: one result per check per round
        builder.HasIndex(s => s.SafetyTypeId);
        builder.HasIndex(s => s.ResultId);
        builder.HasAuditColumns();

        builder.HasOne<Inspection>().WithMany(i => i.SafetyChecks).HasForeignKey(s => s.InspectionId)
            .HasConstraintName("FK_QC_INSP_SAFE_INSP_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SafetyTypeLookup>().WithMany().HasForeignKey(s => s.SafetyTypeId)
            .HasConstraintName("FK_QC_INSP_SAFE_QC_SAFE_TYP_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CheckResultLookup>().WithMany().HasForeignKey(s => s.ResultId)
            .HasConstraintName("FK_QC_INSP_SAFE_QC_CHK_RSLT_ID").OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(SafetyCheck.AffectedLines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SafetyAffectedLineConfiguration : IEntityTypeConfiguration<SafetyAffectedLine>
{
    public void Configure(EntityTypeBuilder<SafetyAffectedLine> builder)
    {
        builder.ToTable("QC_INSP_SAFE_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.SafetyCheckId).HasColumnName("INSP_SAFE_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.HasIndex(l => new { l.SafetyCheckId, l.SizeId, l.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<SafetyCheck>().WithMany(s => s.AffectedLines).HasForeignKey(l => l.SafetyCheckId)
            .HasConstraintName("FK_QC_INSP_SAFE_LINE_INSP_SAFE_ID").OnDelete(DeleteBehavior.Cascade);
    }
}
