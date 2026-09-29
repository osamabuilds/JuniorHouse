using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Vendor.Domain.PurchaseOrders;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

internal sealed class PoLineConfiguration : IEntityTypeConfiguration<PoLine>
{
    public void Configure(EntityTypeBuilder<PoLine> builder)
    {
        builder.ToTable("PO_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.PoId).HasColumnName("PO_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(l => l.Qty).HasColumnName("QTY").IsRequired();
        builder.HasIndex(l => new { l.PoId, l.SizeId, l.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<PurchaseOrder>().WithMany(p => p.Lines)
            .HasForeignKey(l => l.PoId)
            .HasConstraintName("FK_PO_LINE_PO_ID")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
