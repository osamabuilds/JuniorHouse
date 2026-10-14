using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Vendor.Domain.PurchaseOrders;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

internal sealed class PoReceiptConfiguration : IEntityTypeConfiguration<PoReceipt>
{
    public void Configure(EntityTypeBuilder<PoReceipt> builder)
    {
        builder.ToTable("PO_RCVD");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.PoId).HasColumnName("PO_ID").IsRequired();
        builder.HasIndex(r => r.PoId);
        builder.Property(r => r.GrnId).HasColumnName("GRN_ID").IsRequired();
        builder.HasIndex(r => r.GrnId).IsUnique(); // AC-41: one row per goods receipt, however often the event is delivered
        builder.Property(r => r.GrnNo).HasColumnName("GRN_NO").HasMaxLength(20).IsRequired();
        builder.Property(r => r.ReceivedDate).HasColumnName("RCVD_DT").HasColumnType("date");
        builder.Property(r => r.ClaimedTotal).HasColumnName("CLM_TOT_QTY").IsRequired();
        builder.Property(r => r.CountedTotal).HasColumnName("CNTD_TOT_QTY").IsRequired();
        builder.HasAuditColumns();

        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(r => r.PoId)
            .HasConstraintName("FK_PO_RCVD_PO_ID").OnDelete(DeleteBehavior.Restrict);

        builder.Metadata.FindNavigation(nameof(PoReceipt.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PoReceiptLineConfiguration : IEntityTypeConfiguration<PoReceiptLine>
{
    public void Configure(EntityTypeBuilder<PoReceiptLine> builder)
    {
        builder.ToTable("PO_RCVD_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.ReceiptId).HasColumnName("PO_RCVD_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(l => l.ClaimedQty).HasColumnName("CLM_QTY").IsRequired();
        builder.Property(l => l.CountedQty).HasColumnName("CNTD_QTY").IsRequired();
        builder.HasIndex(l => new { l.ReceiptId, l.SizeId, l.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<PoReceipt>().WithMany(r => r.Lines).HasForeignKey(l => l.ReceiptId)
            .HasConstraintName("FK_PO_RCVD_LINE_PO_RCVD_ID").OnDelete(DeleteBehavior.Cascade);
    }
}
