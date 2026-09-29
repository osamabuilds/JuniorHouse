using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Infrastructure.Vendors;

internal sealed class ScorecardDeliveryConfiguration : IEntityTypeConfiguration<ScorecardDelivery>
{
    public void Configure(EntityTypeBuilder<ScorecardDelivery> builder)
    {
        builder.ToTable("SCRD_DLVR");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(d => d.VendorId).HasColumnName("VNDR_ID").IsRequired();
        builder.HasIndex(d => d.VendorId);
        builder.Property(d => d.PoId).HasColumnName("PO_ID").IsRequired();
        builder.Property(d => d.VendorDnNo).HasColumnName("VNDR_DN_NO").HasMaxLength(50).IsRequired();
        builder.HasIndex(d => new { d.PoId, d.VendorDnNo }).IsUnique(); // AC-38: a delivery is counted once
        builder.Property(d => d.DispatchDate).HasColumnName("DSPT_DT").HasColumnType("date");
        builder.Property(d => d.IsOnTime).HasColumnName("ONTM_IND").IsRequired();
        builder.Property(d => d.IsOnQuantity).HasColumnName("ONQT_IND").IsRequired();
        builder.Property(d => d.IsRompFabricDelay).HasColumnName("ROMP_FBRC_DLAY_IND").IsRequired();
        builder.HasAuditColumns();

        builder.HasOne<Domain.Vendors.Vendor>().WithMany().HasForeignKey(d => d.VendorId)
            .HasConstraintName("FK_SCRD_DLVR_VNDR_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(d => d.PoId)
            .HasConstraintName("FK_SCRD_DLVR_PO_ID").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ScorecardInspectionConfiguration : IEntityTypeConfiguration<ScorecardInspection>
{
    public void Configure(EntityTypeBuilder<ScorecardInspection> builder)
    {
        builder.ToTable("SCRD_INSP");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(i => i.VendorId).HasColumnName("VNDR_ID").IsRequired();
        builder.HasIndex(i => i.VendorId);
        builder.Property(i => i.PoId).HasColumnName("PO_ID").IsRequired();
        builder.HasIndex(i => i.PoId);
        builder.Property(i => i.BatchId).HasColumnName("BTCH_ID").IsRequired();
        builder.HasIndex(i => i.BatchId).IsUnique(); // AC-38: a batch's first pass is counted once
        builder.Property(i => i.InspectionDate).HasColumnName("INSP_DT").HasColumnType("date");
        builder.Property(i => i.InspectedQty).HasColumnName("INSP_QTY").IsRequired();
        builder.Property(i => i.DefectiveQty).HasColumnName("DFCT_QTY").IsRequired();
        builder.HasAuditColumns();

        builder.HasOne<Domain.Vendors.Vendor>().WithMany().HasForeignKey(i => i.VendorId)
            .HasConstraintName("FK_SCRD_INSP_VNDR_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PurchaseOrder>().WithMany().HasForeignKey(i => i.PoId)
            .HasConstraintName("FK_SCRD_INSP_PO_ID").OnDelete(DeleteBehavior.Restrict);
    }
}
