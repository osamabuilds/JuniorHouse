using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Bins;
using Romp.Modules.Warehouse.Domain.Putaway;
using Romp.Modules.Warehouse.Domain.StockLedger;

namespace Romp.Modules.Warehouse.Infrastructure.Putaway;

internal sealed class PutawayItemConfiguration : IEntityTypeConfiguration<PutawayItem>
{
    public void Configure(EntityTypeBuilder<PutawayItem> builder)
    {
        builder.ToTable("PUT_ITEM", table =>
        {
            table.HasCheckConstraint("CK_PUT_ITEM_QC_QTY_POS", "\"QC_QTY\" > 0");
            table.HasCheckConstraint("CK_PUT_ITEM_PUT_QTY_RANGE", "\"PUT_QTY\" >= 0 AND \"PUT_QTY\" <= \"QC_QTY\"");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(i => i.SubBatchId).HasColumnName("SUB_BTCH_ID").IsRequired();
        builder.Property(i => i.QcBatchId).HasColumnName("QC_BTCH_ID").IsRequired();
        builder.Property(i => i.PoId).HasColumnName("PO_ID").IsRequired();
        builder.Property(i => i.StyleId).HasColumnName("STYL_ID").IsRequired();
        builder.Property(i => i.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(i => i.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(i => i.GradeId).HasColumnName("GRD_ID").IsRequired();
        builder.Property(i => i.QcQuantity).HasColumnName("QC_QTY").IsRequired();
        builder.Property(i => i.PutQuantity).HasColumnName("PUT_QTY").IsRequired();
        builder.Property(i => i.VariantRefId).HasColumnName("VRNT_REF_ID");
        builder.Property(i => i.StatusId).HasColumnName("PUT_STS_ID").IsRequired();
        builder.Ignore(i => i.Pending);

        // PUT-2: one item per (sub-batch, size, colour). A redelivered QC message cannot create a second one.
        builder.HasIndex(i => new { i.SubBatchId, i.SizeId, i.ColourId }).IsUnique();
        builder.HasIndex(i => new { i.StatusId, i.Id });
        builder.HasIndex(i => i.VariantRefId);
        builder.HasIndex(i => new { i.StyleId, i.ColourId, i.SizeId });
        builder.HasAuditColumns();

        builder.HasOne<VariantRef>().WithMany().HasForeignKey(i => i.VariantRefId).HasConstraintName("FK_PUT_ITEM_VRNT_REF").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GradeLookup>().WithMany().HasForeignKey(i => i.GradeId).HasConstraintName("FK_PUT_ITEM_GRD_LKP");
        builder.HasOne<PutawayStatusLookup>().WithMany().HasForeignKey(i => i.StatusId).HasConstraintName("FK_PUT_ITEM_PUT_STS_LKP");
    }
}

internal sealed class PutawayRecordConfiguration : IEntityTypeConfiguration<PutawayRecord>
{
    public void Configure(EntityTypeBuilder<PutawayRecord> builder)
    {
        builder.ToTable("PUT_RCRD", table => table.HasCheckConstraint("CK_PUT_RCRD_QTY_POS", "\"QTY\" > 0"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.ItemId).HasColumnName("PUT_ITEM_ID").IsRequired();
        builder.Property(r => r.BinId).HasColumnName("BIN_ID").IsRequired();
        builder.Property(r => r.Quantity).HasColumnName("QTY").IsRequired();
        builder.Property(r => r.Scanned).HasColumnName("SCAN_IND").IsRequired();
        builder.Property(r => r.IdempotencyKey).HasColumnName("IDEM_KEY").IsRequired();
        builder.Property(r => r.ActorName).HasColumnName("ACTR_NAME").HasMaxLength(100).IsRequired();

        // PUT-8: the same confirmation sent twice (double scan, network retry) applies once.
        builder.HasIndex(r => r.IdempotencyKey).IsUnique();
        builder.HasIndex(r => r.ItemId);
        builder.HasIndex(r => r.BinId);
        builder.HasAuditColumns();

        builder.HasOne<PutawayItem>().WithMany().HasForeignKey(r => r.ItemId).HasConstraintName("FK_PUT_RCRD_PUT_ITEM").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bin>().WithMany().HasForeignKey(r => r.BinId).HasConstraintName("FK_PUT_RCRD_BIN_MAIN").OnDelete(DeleteBehavior.Restrict);
    }
}
