using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Vendor.Domain.PurchaseOrders;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PO_MAIN");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.PoNo).HasColumnName("PO_NO").HasMaxLength(15).IsRequired();
        builder.HasIndex(p => p.PoNo).IsUnique();
        builder.Property(p => p.VendorId).HasColumnName("VNDR_ID").IsRequired();
        builder.HasIndex(p => p.VendorId);
        builder.Property(p => p.StyleId).HasColumnName("STYL_ID").IsRequired();
        builder.HasIndex(p => p.StyleId);
        builder.Property(p => p.UnitCost).HasColumnName("UNIT_COST_AMT").HasColumnType("numeric(12,2)");
        builder.Property(p => p.ExpectedDeliveryDate).HasColumnName("EXPC_DLVR_DT").HasColumnType("date");
        builder.Property(p => p.PaymentTermId).HasColumnName("PAYM_TERM_ID").IsRequired();
        builder.HasIndex(p => p.PaymentTermId);
        builder.Property(p => p.AdvancePercent).HasColumnName("ADV_PCT").HasColumnType("numeric(5,2)");
        builder.Property(p => p.LatestAcceptableDate).HasColumnName("LATE_ACPT_DT").HasColumnType("date");
        builder.Property(p => p.OverTolerancePercent).HasColumnName("OVER_TOL_PCT").HasColumnType("numeric(5,2)");
        builder.Property(p => p.UnderTolerancePercent).HasColumnName("UNDR_TOL_PCT").HasColumnType("numeric(5,2)");
        builder.Property(p => p.FabricResponsibilityId).HasColumnName("FBRC_RESP_ID");
        builder.HasIndex(p => p.FabricResponsibilityId);
        builder.Property(p => p.StatusId).HasColumnName("PO_STS_ID").IsRequired();
        builder.HasIndex(p => p.StatusId);
        builder.Ignore(p => p.IsDraft);
        builder.HasAuditColumns();

        builder.Metadata.FindNavigation(nameof(PurchaseOrder.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(PurchaseOrder.StatusHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(PurchaseOrder.Revisions))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(PurchaseOrder.VendorCommunications))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
