using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Infrastructure.Vendors;

internal sealed class VendorSpecialisationConfiguration : IEntityTypeConfiguration<VendorSpecialisation>
{
    public void Configure(EntityTypeBuilder<VendorSpecialisation> builder)
    {
        builder.ToTable("VNDR_SPCL_MAP");
        builder.HasKey(s => new { s.VendorId, s.SpecialisationId });
        builder.Property(s => s.VendorId).HasColumnName("VNDR_ID");
        builder.Property(s => s.SpecialisationId).HasColumnName("SPCL_ID");
        builder.HasIndex(s => s.SpecialisationId);

        builder.HasOne<Domain.Vendors.Vendor>().WithMany(v => v.Specialisations)
            .HasForeignKey(s => s.VendorId)
            .HasConstraintName("FK_VNDR_SPCL_MAP_VNDR_ID")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
