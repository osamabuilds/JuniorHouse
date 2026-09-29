using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Catalog.Domain.Styles;

namespace Romp.Modules.Catalog.Infrastructure.Styles;

internal sealed class StyleSizeConfiguration : IEntityTypeConfiguration<StyleSize>
{
    public void Configure(EntityTypeBuilder<StyleSize> builder)
    {
        builder.ToTable("STYL_SIZE_MAP");
        builder.HasKey(s => new { s.StyleId, s.SizeId });
        builder.Property(s => s.StyleId).HasColumnName("STYL_ID");
        builder.Property(s => s.SizeId).HasColumnName("SIZE_ID");
        builder.HasIndex(s => s.SizeId);

        builder.HasOne<Style>().WithMany(s => s.Sizes)
            .HasForeignKey(s => s.StyleId)
            .HasConstraintName("FK_STYL_SIZE_MAP_STYL_ID")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
