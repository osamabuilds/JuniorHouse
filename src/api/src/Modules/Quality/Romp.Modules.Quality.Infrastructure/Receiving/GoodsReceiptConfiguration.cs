using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.InboundDeliveries;
using Romp.Modules.Quality.Domain.Receiving;

namespace Romp.Modules.Quality.Infrastructure.Receiving;

internal sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("GRN_MAIN");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(g => g.GrnNo).HasColumnName("GRN_NO").HasMaxLength(20).IsRequired();
        builder.HasIndex(g => g.GrnNo).IsUnique();
        builder.Property(g => g.DeliveryId).HasColumnName("INBD_DLVR_ID").IsRequired();
        builder.HasIndex(g => g.DeliveryId).IsUnique(); // AC-4: one goods receipt per delivery
        builder.Property(g => g.RunId).HasColumnName("RUN_ID").IsRequired();
        builder.Property(g => g.PoId).HasColumnName("PO_ID").IsRequired();
        builder.HasIndex(g => g.PoId);
        builder.Property(g => g.PoNo).HasColumnName("PO_NO").HasMaxLength(30).IsRequired();
        builder.Property(g => g.VendorDnNo).HasColumnName("VNDR_DN_NO").HasMaxLength(50).IsRequired();
        builder.Property(g => g.ReceivedDate).HasColumnName("RCVD_DT").HasColumnType("date");
        builder.Property(g => g.StatusId).HasColumnName("GRN_STS_ID").IsRequired();
        builder.HasIndex(g => g.StatusId);
        builder.Property(g => g.ReconciliationId).HasColumnName("GRN_RCNL_ID").IsRequired();
        builder.HasIndex(g => g.ReconciliationId);
        builder.Property(g => g.ClaimedTotal).HasColumnName("CLM_TOT_QTY").IsRequired();
        builder.Property(g => g.CountedTotal).HasColumnName("CNTD_TOT_QTY").IsRequired();
        builder.Property(g => g.ActingPerson).HasColumnName("ACTR_NAME").HasMaxLength(100).IsRequired();
        builder.Property(g => g.Note).HasColumnName("NOTE").HasMaxLength(500);
        builder.HasAuditColumns();

        builder.HasOne<InboundDelivery>().WithMany().HasForeignKey(g => g.DeliveryId)
            .HasConstraintName("FK_GRN_MAIN_INBD_DLVR_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GrnStatusLookup>().WithMany().HasForeignKey(g => g.StatusId)
            .HasConstraintName("FK_GRN_MAIN_GRN_STS_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GrnReconciliationLookup>().WithMany().HasForeignKey(g => g.ReconciliationId)
            .HasConstraintName("FK_GRN_MAIN_GRN_RCNL_ID").OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(GoodsReceipt.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable("GRN_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.GrnId).HasColumnName("GRN_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(l => l.ClaimedQty).HasColumnName("CLM_QTY").IsRequired();
        builder.Property(l => l.CountedQty).HasColumnName("CNTD_QTY").IsRequired();
        builder.Property(l => l.DiffQty).HasColumnName("DIFF_QTY").IsRequired();
        builder.Property(l => l.IsExcess).HasColumnName("EXCS_IND").IsRequired();
        builder.HasIndex(l => new { l.GrnId, l.SizeId, l.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<GoodsReceipt>().WithMany(g => g.Lines).HasForeignKey(l => l.GrnId)
            .HasConstraintName("FK_GRN_LINE_GRN_ID").OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GrnNumberSequenceConfiguration : IEntityTypeConfiguration<GrnNumberSequence>
{
    public void Configure(EntityTypeBuilder<GrnNumberSequence> builder)
    {
        // ADR 0006: a private per-year counter, advanced by an atomic upsert (never through EF).
        builder.ToTable("GRN_NO_SEQ");
        builder.HasKey(s => s.Year);
        builder.Property(s => s.Year).HasColumnName("YR").ValueGeneratedNever();
        builder.Property(s => s.LastSeq).HasColumnName("SEQ").IsRequired();
    }
}
