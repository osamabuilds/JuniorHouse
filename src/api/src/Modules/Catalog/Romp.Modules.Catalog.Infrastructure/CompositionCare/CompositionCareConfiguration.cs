using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Catalog.Domain.CompositionCare;
using Romp.Modules.Catalog.Domain.Products;

namespace Romp.Modules.Catalog.Infrastructure.CompositionCare;

internal sealed class CompositionLineConfiguration : IEntityTypeConfiguration<CompositionLine>
{
    public void Configure(EntityTypeBuilder<CompositionLine> builder)
    {
        builder.ToTable("PRDT_CMPS", table => table.HasCheckConstraint("CK_PRDT_CMPS_PCT", "\"PCT\" > 0 AND \"PCT\" <= 100"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(l => l.FibreId).HasColumnName("FIBR_ID").IsRequired();
        builder.Property(l => l.Percent).HasColumnName("PCT").HasColumnType("numeric(4,1)").IsRequired();

        // CMP-6: the same fibre cannot be listed twice for a product.
        builder.HasIndex(l => new { l.ProductId, l.FibreId }).IsUnique();
        builder.HasIndex(l => l.FibreId);
        builder.HasOne<Product>().WithMany().HasForeignKey(l => l.ProductId)
            .HasConstraintName("FK_PRDT_CMPS_PRDT_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasAuditColumns();
    }
}

internal sealed class ProductCareConfiguration : IEntityTypeConfiguration<ProductCare>
{
    public void Configure(EntityTypeBuilder<ProductCare> builder)
    {
        builder.ToTable("PRDT_CARE_MAP");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(c => c.CareId).HasColumnName("CARE_ID").IsRequired();
        builder.HasIndex(c => new { c.ProductId, c.CareId }).IsUnique();
        builder.HasIndex(c => c.CareId);
        builder.HasOne<Product>().WithMany().HasForeignKey(c => c.ProductId)
            .HasConstraintName("FK_PRDT_CARE_MAP_PRDT_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasAuditColumns();
    }
}
