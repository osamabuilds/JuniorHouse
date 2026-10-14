using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;
using Romp.Modules.Warehouse.Domain.Bins;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Infrastructure.Adjustments;

internal sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("ADJ_MAIN", table => table.HasCheckConstraint("CK_ADJ_MAIN_QTY_CHNG_NONZERO", "\"QTY_CHNG\" <> 0"));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.VariantRefId).HasColumnName("VRNT_REF_ID").IsRequired();
        builder.Property(a => a.GradeId).HasColumnName("GRD_ID").IsRequired();
        builder.Property(a => a.BinId).HasColumnName("BIN_ID").IsRequired();
        builder.Property(a => a.QuantityChange).HasColumnName("QTY_CHNG").IsRequired();
        builder.Property(a => a.ReasonId).HasColumnName("ADJ_RSN_ID").IsRequired();
        builder.Property(a => a.Note).HasColumnName("NOTE").HasMaxLength(200);
        builder.Property(a => a.Reconfirmed).HasColumnName("RCNF_IND").IsRequired();
        builder.Property(a => a.IdempotencyKey).HasColumnName("IDEM_KEY").IsRequired();
        builder.Property(a => a.CountLineId).HasColumnName("CNT_LINE_ID");
        builder.Property(a => a.ActorName).HasColumnName("ACTR_NAME").HasMaxLength(100).IsRequired();

        // ADJ-8: the same submission sent twice applies once.
        builder.HasIndex(a => a.IdempotencyKey).IsUnique();
        builder.HasIndex(a => new { a.VariantRefId, a.Id }).IsDescending(false, true).HasDatabaseName("IX_ADJ_MAIN_VRNT_REF_ID_ID");
        builder.HasIndex(a => a.ReasonId);
        builder.HasIndex(a => a.BinId);
        builder.HasIndex(a => a.InsrDte);
        builder.HasAuditColumns();

        builder.HasOne<VariantRef>().WithMany().HasForeignKey(a => a.VariantRefId).HasConstraintName("FK_ADJ_MAIN_VRNT_REF").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GradeLookup>().WithMany().HasForeignKey(a => a.GradeId).HasConstraintName("FK_ADJ_MAIN_GRD_LKP");
        builder.HasOne<Bin>().WithMany().HasForeignKey(a => a.BinId).HasConstraintName("FK_ADJ_MAIN_BIN_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdjustmentReasonLookup>().WithMany().HasForeignKey(a => a.ReasonId).HasConstraintName("FK_ADJ_MAIN_ADJ_RSN_LKP");
    }
}
