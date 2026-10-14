using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Catalog.Domain.ProductMedia;
using Romp.Modules.Catalog.Domain.Products;

namespace Romp.Modules.Catalog.Infrastructure.ProductMedia;

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("PRDT_IMG", table =>
        {
            // MED-3, MED-5: alt text is either absent (a draft) or 1..125 characters of real text.
            table.HasCheckConstraint("CK_PRDT_IMG_ALT", "\"ALT\" IS NULL OR char_length(btrim(\"ALT\")) BETWEEN 1 AND 125");
            table.HasCheckConstraint("CK_PRDT_IMG_SIZE", "\"WDTH\" > 0 AND \"HGHT\" > 0 AND \"FILE_BYT\" > 0");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(i => i.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(i => i.ColourId).HasColumnName("CLR_ID");
        builder.Property(i => i.FileKey).HasColumnName("FILE_KEY").HasMaxLength(100).IsRequired();
        builder.Property(i => i.ContentType).HasColumnName("CNTT_TYP").HasMaxLength(30).IsRequired();
        builder.Property(i => i.OriginalFileName).HasColumnName("FILE_NAME").HasMaxLength(200).IsRequired();
        builder.Property(i => i.ByteSize).HasColumnName("FILE_BYT").IsRequired();
        builder.Property(i => i.Width).HasColumnName("WDTH").IsRequired();
        builder.Property(i => i.Height).HasColumnName("HGHT").IsRequired();
        builder.Property(i => i.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(i => i.AltText).HasColumnName("ALT").HasMaxLength(125);

        builder.HasIndex(i => new { i.ProductId, i.SortSeq });
        builder.HasIndex(i => i.ColourId);
        builder.HasOne<Product>().WithMany().HasForeignKey(i => i.ProductId)
            .HasConstraintName("FK_PRDT_IMG_PRDT_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasAuditColumns();
    }
}
