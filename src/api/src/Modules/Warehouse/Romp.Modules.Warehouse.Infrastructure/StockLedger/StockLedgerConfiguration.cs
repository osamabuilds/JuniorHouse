using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Bins;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Infrastructure.StockLedger;

internal sealed class VariantRefConfiguration : IEntityTypeConfiguration<VariantRef>
{
    public void Configure(EntityTypeBuilder<VariantRef> builder)
    {
        builder.ToTable("VRNT_REF");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(v => v.VariantId).HasColumnName("VRNT_ID").IsRequired();
        builder.Property(v => v.StyleId).HasColumnName("STYL_ID").IsRequired();
        builder.Property(v => v.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(v => v.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(v => v.SkuCode).HasColumnName("SKU_CODE").HasMaxLength(120).IsRequired();
        builder.Property(v => v.Gtin).HasColumnName("GTIN").HasMaxLength(13).IsFixedLength().IsRequired();
        builder.Property(v => v.GtinRetired).HasColumnName("GTIN_RETD_IND").IsRequired();

        // One reference per Labelling variant, found by the number printed on the tag (the scan lookup, NFR-SC-01).
        builder.HasIndex(v => v.VariantId).IsUnique();
        builder.HasIndex(v => v.Gtin);
        builder.HasIndex(v => new { v.StyleId, v.ColourId, v.SizeId });
        builder.HasAuditColumns();
    }
}

internal sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("STCK_BAL", table => table.HasCheckConstraint("CK_STCK_BAL_QTY_NONNEG", "\"QTY\" >= 0"));
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(b => b.VariantRefId).HasColumnName("VRNT_REF_ID").IsRequired();
        builder.Property(b => b.GradeId).HasColumnName("GRD_ID").IsRequired();
        builder.Property(b => b.BinId).HasColumnName("BIN_ID").IsRequired();
        builder.Property(b => b.Quantity).HasColumnName("QTY").IsRequired();

        // ADR 0012: one balance row per variant, grade and bin; the posting SQL upserts on exactly this key.
        builder.HasIndex(b => new { b.VariantRefId, b.GradeId, b.BinId }).IsUnique();
        builder.HasIndex(b => b.BinId);
        builder.HasAuditColumns();

        builder.HasOne<VariantRef>().WithMany().HasForeignKey(b => b.VariantRefId).HasConstraintName("FK_STCK_BAL_VRNT_REF").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GradeLookup>().WithMany().HasForeignKey(b => b.GradeId).HasConstraintName("FK_STCK_BAL_GRD_LKP");
        builder.HasOne<Bin>().WithMany().HasForeignKey(b => b.BinId).HasConstraintName("FK_STCK_BAL_BIN_MAIN").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("STCK_MVMT", table =>
        {
            table.HasCheckConstraint("CK_STCK_MVMT_QTY_CHNG_NONZERO", "\"QTY_CHNG\" <> 0");
            table.HasCheckConstraint("CK_STCK_MVMT_BALANCES", "\"AFT_QTY\" = \"BEF_QTY\" + \"QTY_CHNG\" AND \"AFT_QTY\" >= 0");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(m => m.VariantRefId).HasColumnName("VRNT_REF_ID").IsRequired();
        builder.Property(m => m.GradeId).HasColumnName("GRD_ID").IsRequired();
        builder.Property(m => m.BinId).HasColumnName("BIN_ID").IsRequired();
        builder.Property(m => m.KindId).HasColumnName("MVMT_KIND_ID").IsRequired();
        builder.Property(m => m.SourceId).HasColumnName("REF_ID").IsRequired();
        builder.Property(m => m.QuantityChange).HasColumnName("QTY_CHNG").IsRequired();
        builder.Property(m => m.BalanceBefore).HasColumnName("BEF_QTY").IsRequired();
        builder.Property(m => m.BalanceAfter).HasColumnName("AFT_QTY").IsRequired();
        builder.Property(m => m.ActorName).HasColumnName("ACTR_NAME").HasMaxLength(100).IsRequired();

        // History reads are "newest first for a variant" and "newest first for a bin" (LED-9). Partitioning key: INSR_DTE (ADR 0012).
        builder.HasIndex(m => new { m.VariantRefId, m.Id }).IsDescending(false, true).HasDatabaseName("IX_STCK_MVMT_VRNT_REF_ID_ID");
        builder.HasIndex(m => new { m.BinId, m.Id }).IsDescending(false, true).HasDatabaseName("IX_STCK_MVMT_BIN_ID_ID");
        builder.HasAuditColumns();

        builder.HasOne<VariantRef>().WithMany().HasForeignKey(m => m.VariantRefId).HasConstraintName("FK_STCK_MVMT_VRNT_REF").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GradeLookup>().WithMany().HasForeignKey(m => m.GradeId).HasConstraintName("FK_STCK_MVMT_GRD_LKP");
        builder.HasOne<Bin>().WithMany().HasForeignKey(m => m.BinId).HasConstraintName("FK_STCK_MVMT_BIN_MAIN").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MovementKindLookup>().WithMany().HasForeignKey(m => m.KindId).HasConstraintName("FK_STCK_MVMT_MVMT_KIND_LKP");
    }
}
