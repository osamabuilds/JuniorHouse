using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Infrastructure.Vendors;

internal sealed class VendorConfiguration : IEntityTypeConfiguration<Domain.Vendors.Vendor>
{
    public void Configure(EntityTypeBuilder<Domain.Vendors.Vendor> builder)
    {
        builder.ToTable("VNDR", "VNDR");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(v => v.Name).HasColumnName("VNDR_NAME").HasMaxLength(200).IsRequired();
        builder.Property(v => v.ContactName).HasColumnName("CNTC_NAME").HasMaxLength(100).IsRequired();
        builder.Property(v => v.ContactPhone).HasColumnName("CNTC_PHON").HasMaxLength(20).IsRequired();
        builder.Property(v => v.ContactEmail).HasColumnName("CNTC_EML").HasMaxLength(200);
        builder.Property(v => v.CityId).HasColumnName("CITY_ID").IsRequired();
        builder.HasIndex(v => v.CityId);
        builder.Property(v => v.PaymentTermId).HasColumnName("PAYM_TERM_ID").IsRequired();
        builder.HasIndex(v => v.PaymentTermId);
        builder.Property(v => v.OnTimePercent).HasColumnName("ONTM_PCT").HasColumnType("numeric(5,2)");
        builder.Property(v => v.OnQuantityPercent).HasColumnName("ONQT_PCT").HasColumnType("numeric(5,2)");
        builder.Property(v => v.DefectRatePercent).HasColumnName("DFCT_RATE_PCT").HasColumnType("numeric(5,2)");
        builder.Property(v => v.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasAuditColumns();

        builder.Metadata.FindNavigation(nameof(Domain.Vendors.Vendor.Specialisations))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
