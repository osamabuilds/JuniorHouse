using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Vendor.Domain.Revisions;

namespace Romp.Modules.Vendor.Infrastructure.Revisions;

internal sealed class PoRevisionStatusHistoryEntryConfiguration : IEntityTypeConfiguration<PoRevisionStatusHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PoRevisionStatusHistoryEntry> builder)
    {
        builder.ToTable("PO_REV_STS_HIST");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(h => h.RevisionId).HasColumnName("PO_REV_ID").IsRequired();
        builder.Property(h => h.FromStatusId).HasColumnName("FROM_STS_ID");
        builder.Property(h => h.ToStatusId).HasColumnName("TO_STS_ID").IsRequired();
        builder.Property(h => h.Note).HasColumnName("NOTE").HasMaxLength(2000);
        builder.Property(h => h.InsrDte).HasColumnName("INSR_DTE").IsRequired();
        builder.Property(h => h.InsrBy).HasColumnName("INSR_BY").HasMaxLength(100).IsRequired();
        builder.Ignore(h => h.UpdtDte);
        builder.Ignore(h => h.UpdtBy);
        builder.HasIndex(h => h.RevisionId);

        builder.HasOne<PurchaseOrderRevision>().WithMany(r => r.StatusHistory)
            .HasForeignKey(h => h.RevisionId)
            .HasConstraintName("FK_PO_REV_STS_HIST_PO_REV_ID")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
