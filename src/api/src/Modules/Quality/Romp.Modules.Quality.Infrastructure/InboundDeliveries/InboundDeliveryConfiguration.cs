using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Quality.Domain.InboundDeliveries;

namespace Romp.Modules.Quality.Infrastructure.InboundDeliveries;

internal sealed class InboundDeliveryConfiguration : IEntityTypeConfiguration<InboundDelivery>
{
    public void Configure(EntityTypeBuilder<InboundDelivery> builder)
    {
        builder.ToTable("INBD_DLVR");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(d => d.RunId).HasColumnName("RUN_ID").IsRequired();
        builder.Property(d => d.PoId).HasColumnName("PO_ID").IsRequired();
        builder.HasIndex(d => d.PoId);
        builder.Property(d => d.PoNo).HasColumnName("PO_NO").HasMaxLength(30).IsRequired();
        builder.Property(d => d.VendorDnNo).HasColumnName("VNDR_DN_NO").HasMaxLength(50).IsRequired();
        builder.HasIndex(d => new { d.RunId, d.VendorDnNo }).IsUnique(); // AC-2
        builder.Property(d => d.DispatchDate).HasColumnName("DSPT_DT").HasColumnType("date");
        builder.Property(d => d.IsFinalShipment).HasColumnName("FNL_SHIP_IND").IsRequired();
        builder.Property(d => d.IsCancelled).HasColumnName("CNCL_IND").IsRequired();
        builder.HasAuditColumns();

        builder.Metadata.FindNavigation(nameof(InboundDelivery.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class InboundDeliveryLineConfiguration : IEntityTypeConfiguration<InboundDeliveryLine>
{
    public void Configure(EntityTypeBuilder<InboundDeliveryLine> builder)
    {
        builder.ToTable("INBD_DLVR_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.DeliveryId).HasColumnName("INBD_DLVR_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(l => l.ClaimedQty).HasColumnName("CLM_QTY").IsRequired();
        builder.HasIndex(l => new { l.DeliveryId, l.SizeId, l.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<InboundDelivery>().WithMany(d => d.Lines).HasForeignKey(l => l.DeliveryId)
            .HasConstraintName("FK_INBD_DLVR_LINE_INBD_DLVR_ID").OnDelete(DeleteBehavior.Cascade);
    }
}
