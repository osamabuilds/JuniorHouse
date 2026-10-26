using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Infrastructure.Accounts;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("CUST_MAIN");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();

        // Stored lower-case by the domain, so a plain unique index is case-insensitive.
        builder.Property(c => c.Email).HasColumnName("EML").HasMaxLength(Customer.EmailMaxLength).IsRequired();
        builder.HasIndex(c => c.Email).IsUnique();
        builder.Property(c => c.FirstName).HasColumnName("FRST_NAME").HasMaxLength(Customer.NameMaxLength).IsRequired();
        builder.Property(c => c.LastName).HasColumnName("LAST_NAME").HasMaxLength(Customer.NameMaxLength).IsRequired();
        builder.Property(c => c.Phone).HasColumnName("PHON").HasMaxLength(Customer.PhoneMaxLength);
        builder.Ignore(c => c.DomainEvents);

        builder.HasAuditColumns();
    }
}
