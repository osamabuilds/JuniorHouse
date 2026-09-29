using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Catalog.Domain.Styles;

namespace Romp.Modules.Catalog.Infrastructure.Styles;

internal sealed class StyleConfiguration : IEntityTypeConfiguration<Style>
{
    public void Configure(EntityTypeBuilder<Style> builder)
    {
        builder.ToTable("STYL_MAIN");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.Code).HasColumnName("STYL_CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(s => s.Code).IsUnique();
        builder.Property(s => s.Name).HasColumnName("STYL_NAME").HasMaxLength(200).IsRequired();
        builder.Property(s => s.CollectionName).HasColumnName("COLN_NAME").HasMaxLength(100);
        builder.Property(s => s.CategoryId).HasColumnName("CATG_ID").IsRequired();
        builder.HasIndex(s => s.CategoryId);
        builder.Property(s => s.GenderId).HasColumnName("GNDR_ID").IsRequired();
        builder.HasIndex(s => s.GenderId);
        builder.Property(s => s.AgeBracketId).HasColumnName("AGE_BRKT_ID").IsRequired();
        builder.HasIndex(s => s.AgeBracketId);
        builder.Property(s => s.FabricId).HasColumnName("FBRC_ID").IsRequired();
        builder.HasIndex(s => s.FabricId);
        builder.Property(s => s.TargetUnitCost).HasColumnName("TGT_UNIT_COST_AMT").HasColumnType("numeric(12,2)");
        builder.Property(s => s.TargetRetailPrice).HasColumnName("TGT_RTL_PRIC_AMT").HasColumnType("numeric(12,2)");
        builder.Property(s => s.IsActive).HasColumnName("ACT_IND").IsRequired();
        builder.HasAuditColumns();

        builder.Metadata.FindNavigation(nameof(Style.Colourways))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Style.Sizes))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Style.TargetLines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
