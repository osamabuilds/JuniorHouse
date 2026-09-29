using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Catalog.Domain.Styles;

namespace Romp.Modules.Catalog.Infrastructure.Styles;

internal sealed class StyleColourwayConfiguration : IEntityTypeConfiguration<StyleColourway>
{
    public void Configure(EntityTypeBuilder<StyleColourway> builder)
    {
        builder.ToTable("STYL_CLR_MAP");
        builder.HasKey(c => new { c.StyleId, c.ColourId });
        builder.Property(c => c.StyleId).HasColumnName("STYL_ID");
        builder.Property(c => c.ColourId).HasColumnName("CLR_ID");
        builder.HasIndex(c => c.ColourId);

        builder.HasOne<Style>().WithMany(s => s.Colourways)
            .HasForeignKey(c => c.StyleId)
            .HasConstraintName("FK_STYL_CLR_MAP_STYL_ID")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
