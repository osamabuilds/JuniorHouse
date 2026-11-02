using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Domain;
using Romp.Modules.Customers.Domain.Addresses;

namespace Romp.Modules.Customers.Infrastructure.Addresses;

internal sealed class AddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CUST_ADDR", table =>
        {
            // The domain enforces these first; the database refuses the same values if something ever bypasses it (ADD-5).
            table.HasCheckConstraint("CK_CUST_ADDR_RCPT", "char_length(btrim(\"RCPT_NAME\")) BETWEEN 2 AND 80");
            table.HasCheckConstraint("CK_CUST_ADDR_PHON", "\"PHON\" ~ '^\\+92[2-9][0-9]{9}$'");
            table.HasCheckConstraint("CK_CUST_ADDR_LINE", "char_length(btrim(\"LINE_ONE\")) BETWEEN 5 AND 150 AND (\"LINE_TWO\" IS NULL OR char_length(btrim(\"LINE_TWO\")) BETWEEN 1 AND 150)");
            table.HasCheckConstraint("CK_CUST_ADDR_PCDE", "\"PCDE\" IS NULL OR \"PCDE\" ~ '^[A-Za-z0-9]{1,10}$'");
        });
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.CustomerId).HasColumnName("CUST_ID").IsRequired();
        builder.Property(a => a.RecipientName).HasColumnName("RCPT_NAME").HasMaxLength(CustomerAddress.RecipientMaxLength).IsRequired();
        builder.Property(a => a.Phone).HasColumnName("PHON").HasMaxLength(PhoneNumber.MaxLength).IsRequired();
        builder.Property(a => a.ProvinceId).HasColumnName("PROV_ID").IsRequired();
        builder.Property(a => a.CityId).HasColumnName("CITY_ID").IsRequired();
        builder.Property(a => a.LineOne).HasColumnName("LINE_ONE").HasMaxLength(CustomerAddress.LineMaxLength).IsRequired();
        builder.Property(a => a.LineTwo).HasColumnName("LINE_TWO").HasMaxLength(CustomerAddress.LineMaxLength);
        builder.Property(a => a.Postcode).HasColumnName("PCDE").HasMaxLength(CustomerAddress.PostcodeMaxLength);
        builder.Property(a => a.IsDefault).HasColumnName("DFLT_IND").IsRequired();

        builder.HasIndex(a => a.CustomerId, "IX_CUST_ADDR_CUST_ID");

        // At most one default per customer; the address book enforces it first, this is the backstop under concurrency (ADD-2).
        builder.HasIndex(a => a.CustomerId, "UQ_CUST_ADDR_DFLT").IsUnique().HasFilter("\"DFLT_IND\"");

        // Province and city are plain references into the Reference lookups: no foreign key crosses a schema (ADR 0002).
        builder.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId)
            .HasConstraintName("FK_CUST_ADDR_CUST_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}
