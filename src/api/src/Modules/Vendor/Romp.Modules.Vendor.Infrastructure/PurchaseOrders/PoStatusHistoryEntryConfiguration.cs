using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Vendor.Domain.PurchaseOrders;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

internal sealed class PoStatusHistoryEntryConfiguration : IEntityTypeConfiguration<PoStatusHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PoStatusHistoryEntry> builder)
    {
        builder.ToTable("PO_STS_HIST");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(h => h.PoId).HasColumnName("PO_ID").IsRequired();
        builder.Property(h => h.PoStatusId).HasColumnName("PO_STS_ID").IsRequired();
        builder.Property(h => h.CancelReasonId).HasColumnName("PO_CNCL_RSN_ID");
        builder.Property(h => h.Note).HasColumnName("NOTE").HasMaxLength(500);
        builder.Property(h => h.InsrDte).HasColumnName("INSR_DTE").IsRequired();
        builder.Property(h => h.InsrBy).HasColumnName("INSR_BY").HasMaxLength(100).IsRequired();
        builder.Ignore(h => h.UpdtDte);
        builder.Ignore(h => h.UpdtBy);
        builder.HasIndex(h => h.PoId);

        builder.HasOne<PurchaseOrder>().WithMany(p => p.StatusHistory)
            .HasForeignKey(h => h.PoId)
            .HasConstraintName("FK_PO_STS_HIST_PO_ID")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
