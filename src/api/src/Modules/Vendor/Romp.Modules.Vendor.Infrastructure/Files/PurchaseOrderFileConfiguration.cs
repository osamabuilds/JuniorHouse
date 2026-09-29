using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Vendor.Domain.Files;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Revisions;

namespace Romp.Modules.Vendor.Infrastructure.Files;

internal sealed class PurchaseOrderFileConfiguration : IEntityTypeConfiguration<PurchaseOrderFile>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderFile> builder)
    {
        builder.ToTable("PO_FILE");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(f => f.PoId).HasColumnName("PO_ID").IsRequired();
        builder.HasIndex(f => f.PoId);
        builder.Property(f => f.CategoryId).HasColumnName("CATG_ID").IsRequired();
        builder.HasIndex(f => f.CategoryId);
        builder.Property(f => f.FileName).HasColumnName("FILE_NAME").HasMaxLength(255).IsRequired();
        builder.Property(f => f.StorageKey).HasColumnName("STOR_KEY").HasMaxLength(500).IsRequired();
        builder.Property(f => f.ContentType).HasColumnName("CNTT_TYP").HasMaxLength(100).IsRequired();
        builder.Property(f => f.FileSizeBytes).HasColumnName("FILE_SIZE_BYT").IsRequired();
        builder.Property(f => f.AddedInRevisionId).HasColumnName("ADDD_REV_ID");
        builder.HasIndex(f => f.AddedInRevisionId);
        builder.Property(f => f.RetiredInRevisionId).HasColumnName("RETD_REV_ID");
        builder.HasIndex(f => f.RetiredInRevisionId);
        builder.Property(f => f.IsDeleted).HasColumnName("DELD_IND").IsRequired().HasDefaultValue(false);
        builder.Property(f => f.VendorCommunicationId).HasColumnName("VNDR_COMM_ID");
        builder.HasIndex(f => f.VendorCommunicationId);
        builder.HasAuditColumns();

        builder.HasOne<PurchaseOrder>().WithMany()
            .HasForeignKey(f => f.PoId)
            .HasConstraintName("FK_PO_FILE_PO_ID")
            .OnDelete(DeleteBehavior.Cascade);

        // Not ownership relationships (a file merely references the revision(s) it's effective
        // for, or the communication it's evidence of) - Restrict, not Cascade, since PO_REV/
        // PO_VNDR_COMM rows are permanent history and never deleted in this app.
        builder.HasOne<PurchaseOrderRevision>().WithMany()
            .HasForeignKey(f => f.AddedInRevisionId)
            .HasConstraintName("FK_PO_FILE_ADDD_REV_ID")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PurchaseOrderRevision>().WithMany()
            .HasForeignKey(f => f.RetiredInRevisionId)
            .HasConstraintName("FK_PO_FILE_RETD_REV_ID")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PoVendorCommunication>().WithMany()
            .HasForeignKey(f => f.VendorCommunicationId)
            .HasConstraintName("FK_PO_FILE_VNDR_COMM_ID")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
