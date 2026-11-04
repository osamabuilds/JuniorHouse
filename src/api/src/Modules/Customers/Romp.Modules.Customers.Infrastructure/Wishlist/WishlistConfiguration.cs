using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Domain.Wishlist;

namespace Romp.Modules.Customers.Infrastructure.Wishlist;

internal sealed class WishlistConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("CUST_WSHL", table => table.HasCheckConstraint("CK_CUST_WSHL_SAVD_PRIC_AMT", "\"SAVD_PRIC_AMT\" >= 0"));
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(w => w.CustomerId).HasColumnName("CUST_ID").IsRequired();

        // The Catalog product id as the storefront read model exposes it: a plain number, no foreign key across a schema (ADR 0002).
        builder.Property(w => w.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(w => w.SavedPrice).HasColumnName("SAVD_PRIC_AMT").HasColumnType("numeric(12,2)").IsRequired();

        builder.HasIndex(w => w.CustomerId, "IX_CUST_WSHL_CUST_ID");

        // One row per customer and product; adding again is a no-op, so a repeat or a race never makes two (WSL-4).
        builder.HasIndex(w => new { w.CustomerId, w.ProductId }, "UQ_CUST_WSHL_CUST_ID_PRDT_ID").IsUnique();

        builder.HasOne<Customer>().WithMany().HasForeignKey(w => w.CustomerId)
            .HasConstraintName("FK_CUST_WSHL_CUST_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}
