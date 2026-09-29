using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Revisions;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

internal sealed class PoVendorCommunicationConfiguration : IEntityTypeConfiguration<PoVendorCommunication>
{
    public void Configure(EntityTypeBuilder<PoVendorCommunication> builder)
    {
        builder.ToTable("PO_VNDR_COMM");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.PoId).HasColumnName("PO_ID").IsRequired();
        builder.HasIndex(c => c.PoId);
        builder.Property(c => c.RevisionId).HasColumnName("PO_REV_ID");
        builder.HasIndex(c => c.RevisionId);
        builder.Property(c => c.CommunicationTypeId).HasColumnName("COMM_TYP_ID").IsRequired();
        builder.HasIndex(c => c.CommunicationTypeId);
        builder.Property(c => c.ChannelId).HasColumnName("CHNL_ID").IsRequired();
        builder.HasIndex(c => c.ChannelId);
        builder.Property(c => c.ResponderName).HasColumnName("RSPR_NAME").HasMaxLength(200).IsRequired();
        builder.Property(c => c.ResponseDte).HasColumnName("RSPN_DTE").IsRequired();
        builder.HasAuditColumns();

        builder.HasOne<PurchaseOrder>().WithMany(p => p.VendorCommunications)
            .HasForeignKey(c => c.PoId)
            .HasConstraintName("FK_PO_VNDR_COMM_PO_ID")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PurchaseOrderRevision>().WithMany()
            .HasForeignKey(c => c.RevisionId)
            .HasConstraintName("FK_PO_VNDR_COMM_PO_REV_ID")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
