using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Cart.Domain.Carts;

namespace Romp.Modules.Cart.Infrastructure.Carts;

internal sealed class CartConfiguration : IEntityTypeConfiguration<ShoppingCart>
{
    public void Configure(EntityTypeBuilder<ShoppingCart> builder)
    {
        builder.ToTable("CART_MAIN", table =>
            // A cart belongs to exactly one owner: a customer or a guest, never both and never neither (CRT-9). The domain enforces it
            // first; the database refuses the same if something ever bypasses it.
            table.HasCheckConstraint("CK_CART_MAIN_OWNR", "(\"CUST_ID\" IS NULL) <> (\"GST_KEY_HASH\" IS NULL)"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.CustomerId).HasColumnName("CUST_ID");
        builder.Property(c => c.GuestKeyHash).HasColumnName("GST_KEY_HASH").HasMaxLength(64);
        builder.Property(c => c.AppliedCodeId).HasColumnName("DISC_CODE_ID");
        builder.Property(c => c.LastTouchedAt).HasColumnName("LAST_USED_DTE").IsRequired();
        builder.Property(c => c.ExpiresAt).HasColumnName("EXPY_DTE");

        // An owner has at most one cart. The customer id is a plain reference to Customers (no key crosses a schema, ADR 0002).
        builder.HasIndex(c => c.CustomerId, "UQ_CART_MAIN_CUST_ID").IsUnique().HasFilter("\"CUST_ID\" IS NOT NULL");
        builder.HasIndex(c => c.GuestKeyHash, "UQ_CART_MAIN_GST_KEY_HASH").IsUnique().HasFilter("\"GST_KEY_HASH\" IS NOT NULL");

        // The purge job finds expired guest carts by this date.
        builder.HasIndex(c => c.ExpiresAt, "IX_CART_MAIN_EXPY_DTE").HasFilter("\"EXPY_DTE\" IS NOT NULL");

        builder.HasMany(c => c.Lines).WithOne().HasForeignKey(l => l.CartId)
            .HasConstraintName("FK_CART_LINE_CART_ID").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(c => c.IsEmpty);
        builder.Ignore(c => c.DomainEvents);
        builder.HasAuditColumns();
    }
}

internal sealed class CartLineConfiguration : IEntityTypeConfiguration<CartLine>
{
    public void Configure(EntityTypeBuilder<CartLine> builder)
    {
        builder.ToTable("CART_LINE", table => table.HasCheckConstraint("CK_CART_LINE_QTY", "\"QTY\" > 0"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.CartId).HasColumnName("CART_ID").IsRequired();
        builder.Property(l => l.VariantId).HasColumnName("VRNT_ID").IsRequired();
        builder.Property(l => l.Quantity).HasColumnName("QTY").IsRequired();

        // One line per variant in a cart. The variant id is a plain reference to Catalog and Warehouse (ADR 0002).
        builder.HasIndex(l => new { l.CartId, l.VariantId }, "UQ_CART_LINE_CART_ID_VRNT_ID").IsUnique();
        builder.HasAuditColumns();
    }
}
