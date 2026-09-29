using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

internal sealed class PoNumberSequenceConfiguration : IEntityTypeConfiguration<PoNumberSequence>
{
    public void Configure(EntityTypeBuilder<PoNumberSequence> builder)
    {
        builder.ToTable("PO_NO_SEQ");
        builder.HasKey(s => s.Year);
        builder.Property(s => s.Year).HasColumnName("YR");
        builder.Property(s => s.Seq).HasColumnName("SEQ").IsRequired();
    }
}
