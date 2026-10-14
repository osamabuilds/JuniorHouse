using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Labelling.Domain;
using Romp.Modules.Labelling.Domain.LabelWork;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Infrastructure.LabelWork;

internal sealed class LabelWorkItemConfiguration : IEntityTypeConfiguration<LabelWorkItem>
{
    public void Configure(EntityTypeBuilder<LabelWorkItem> builder)
    {
        builder.ToTable("LBL_WORK_ITEM");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(i => i.SubBatchId).HasColumnName("SUB_BTCH_ID").IsRequired();
        builder.Property(i => i.QcBatchId).HasColumnName("QC_BTCH_ID").IsRequired();
        builder.Property(i => i.PoId).HasColumnName("PO_ID").IsRequired();
        builder.Property(i => i.StyleId).HasColumnName("STYL_ID");
        builder.Property(i => i.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(i => i.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(i => i.Qty).HasColumnName("QTY").IsRequired();
        builder.Property(i => i.OutcomeId).HasColumnName("LBL_OUTC_ID").IsRequired();
        builder.Property(i => i.StatusId).HasColumnName("LBL_WORK_STS_ID").IsRequired();
        builder.Property(i => i.ParkReasonId).HasColumnName("PARK_RSN_ID");
        builder.Property(i => i.VariantId).HasColumnName("VRNT_ID");

        // LW-1, LW-2: one work item per sub-batch line, so a redelivered message cannot create it twice.
        builder.HasIndex(i => new { i.SubBatchId, i.SizeId, i.ColourId }).IsUnique();
        builder.HasIndex(i => i.PoId);
        builder.HasIndex(i => new { i.StatusId, i.Id }).HasDatabaseName("IX_LBL_WORK_ITEM_STS_ID");
        builder.HasAuditColumns();

        builder.HasOne<LabelOutcomeLookup>().WithMany().HasForeignKey(i => i.OutcomeId).HasConstraintName("FK_LBL_WORK_ITEM_LBL_OUTC_LKP");
        builder.HasOne<LabelWorkStatusLookup>().WithMany().HasForeignKey(i => i.StatusId).HasConstraintName("FK_LBL_WORK_ITEM_LBL_WORK_STS_LKP");
        builder.HasOne<ParkReasonLookup>().WithMany().HasForeignKey(i => i.ParkReasonId).HasConstraintName("FK_LBL_WORK_ITEM_PARK_RSN_LKP");
        builder.HasOne<Variant>().WithMany().HasForeignKey(i => i.VariantId).HasConstraintName("FK_LBL_WORK_ITEM_VRNT_MAIN").OnDelete(DeleteBehavior.Restrict);
    }
}
