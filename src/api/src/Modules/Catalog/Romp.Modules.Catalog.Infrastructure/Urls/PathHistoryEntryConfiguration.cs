using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Domain.Urls;

namespace Romp.Modules.Catalog.Infrastructure.Urls;

internal sealed class PathHistoryEntryConfiguration : IEntityTypeConfiguration<PathHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PathHistoryEntry> builder)
    {
        builder.ToTable("PRDT_SLUG_HIST", table => table.HasCheckConstraint("CK_PRDT_SLUG_HIST_SLUG", "\"SLUG\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'"));
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(h => h.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(h => h.GenderId).HasColumnName("GNDR_ID").IsRequired();
        builder.Property(h => h.CategoryId).HasColumnName("CATG_ID").IsRequired();
        builder.Property(h => h.Slug).HasColumnName("SLUG").HasMaxLength(80).IsRequired();

        // URL-11: an address belongs to one product for ever, so nobody else can claim an old one. The same unique triple makes the
        // path lookup a single indexed read (NFR-01).
        builder.HasIndex(h => new { h.GenderId, h.CategoryId, h.Slug }).IsUnique();
        builder.HasIndex(h => h.ProductId);
        builder.HasOne<Product>().WithMany().HasForeignKey(h => h.ProductId)
            .HasConstraintName("FK_PRDT_SLUG_HIST_PRDT_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasAuditColumns();
    }
}
