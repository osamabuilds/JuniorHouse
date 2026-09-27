using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Persistence;
using Romp.Modules.Catalog.Application;
using Romp.Modules.Catalog.Domain;

namespace Romp.Modules.Catalog.Infrastructure;

/// <summary>
/// EF Core context for the <c>CTLG</c> schema (style master). Implements
/// <see cref="ICatalogDbContext"/> (Application's own abstraction) so Application-layer command
/// and query handlers can use it without depending on this Infrastructure project (ADR 0002).
/// </summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options), ICatalogDbContext
{
    public DbSet<Style> Styles => Set<Style>();

    async Task ICatalogDbContext.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("CTLG");

        modelBuilder.Entity<Style>(builder =>
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
        });

        modelBuilder.Entity<StyleColourway>(builder =>
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
        });

        modelBuilder.Entity<StyleSize>(builder =>
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
        });

        modelBuilder.Entity<StyleTargetLine>(builder =>
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
        });

        base.OnModelCreating(modelBuilder);
    }
}
