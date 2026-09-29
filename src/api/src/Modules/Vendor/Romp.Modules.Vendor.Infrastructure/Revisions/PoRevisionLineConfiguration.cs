using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Vendor.Domain.Revisions;

namespace Romp.Modules.Vendor.Infrastructure.Revisions;

internal sealed class PoRevisionLineConfiguration : IEntityTypeConfiguration<PoRevisionLine>
{
    public void Configure(EntityTypeBuilder<PoRevisionLine> builder)
    {
        builder.ToTable("PO_REV_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.RevisionId).HasColumnName("PO_REV_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(l => l.Qty).HasColumnName("QTY").IsRequired();
        builder.HasIndex(l => new { l.RevisionId, l.SizeId, l.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<PurchaseOrderRevision>().WithMany(r => r.Lines)
            .HasForeignKey(l => l.RevisionId)
            .HasConstraintName("FK_PO_REV_LINE_PO_REV_ID")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
