using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Catalog.Domain.Styles;

namespace Romp.Modules.Catalog.Infrastructure.Styles;

internal sealed class StyleTargetLineConfiguration : IEntityTypeConfiguration<StyleTargetLine>
{
    public void Configure(EntityTypeBuilder<StyleTargetLine> builder)
    {
        builder.ToTable("STYL_TGT_LINE");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(t => t.StyleId).HasColumnName("STYL_ID");
        builder.Property(t => t.SizeId).HasColumnName("SIZE_ID");
        builder.Property(t => t.ColourId).HasColumnName("CLR_ID");
        builder.Property(t => t.TargetQty).HasColumnName("TGT_QTY");
        builder.HasIndex(t => new { t.StyleId, t.SizeId, t.ColourId }).IsUnique();
        builder.HasAuditColumns();

        builder.HasOne<Style>().WithMany(s => s.TargetLines)
            .HasForeignKey(t => t.StyleId)
            .HasConstraintName("FK_STYL_TGT_LINE_STYL_ID")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
