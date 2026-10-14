using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Domain.Styles;

namespace Romp.Modules.Catalog.Infrastructure.Products;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("PRDT_MAIN", table =>
        {
            // Defence in depth for the slug alphabet (URL-6): the application normalises first, the database has the last word.
            table.HasCheckConstraint("CK_PRDT_MAIN_SLUG", "\"SLUG\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.StyleId).HasColumnName("STYL_ID").IsRequired();
        builder.Property(p => p.Name).HasColumnName("NAME").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasColumnName("DSCR").HasMaxLength(2000).IsRequired();
        builder.Property(p => p.GenderId).HasColumnName("GNDR_ID").IsRequired();
        builder.Property(p => p.CategoryId).HasColumnName("CATG_ID").IsRequired();
        builder.Property(p => p.AgeBracketId).HasColumnName("AGE_BRKT_ID").IsRequired();
        builder.Ignore(p => p.Status);
        builder.Property(p => p.StatusId).HasColumnName("PRDT_STS_ID").IsRequired();
        builder.Property(p => p.Slug).HasColumnName("SLUG").HasMaxLength(80).IsRequired();
        builder.Property(p => p.CareNote).HasColumnName("CARE_NOTE").HasMaxLength(500);
        builder.Property(p => p.PublishedAt).HasColumnName("PUBL_DTE");
        builder.Property(p => p.PublishedBy).HasColumnName("PUBL_BY").HasMaxLength(100);

        // PRD-1: one product per style. The database decides a concurrent create; the handler's check only gives a friendly message.
        builder.HasIndex(p => p.StyleId).IsUnique();

        // URL-6, ADR 0013: a slug is unique within gender + category.
        builder.HasIndex(p => new { p.GenderId, p.CategoryId, p.Slug }).IsUnique();
        builder.HasIndex(p => new { p.StatusId, p.CategoryId });
        builder.HasIndex(p => p.GenderId);
        builder.HasIndex(p => p.AgeBracketId);

        builder.HasOne<Style>().WithOne().HasForeignKey<Product>(p => p.StyleId)
            .HasConstraintName("FK_PRDT_MAIN_STYL_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductStatusLookup>().WithMany().HasForeignKey(p => p.StatusId)
            .HasConstraintName("FK_PRDT_MAIN_PRDT_STS_ID").OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Variants).WithOne().HasForeignKey(v => v.ProductId)
            .HasConstraintName("FK_PRDT_VRNT_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Collections).WithOne().HasForeignKey(c => c.ProductId)
            .HasConstraintName("FK_PRDT_COLN_MAP_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Product.Variants))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Product.Collections))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasAuditColumns();
    }
}

internal sealed class ProductStatusLookupConfiguration : IEntityTypeConfiguration<ProductStatusLookup>
{
    public void Configure(EntityTypeBuilder<ProductStatusLookup> builder)
    {
        builder.ToTable("PRDT_STS_LKP");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(l => l.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(l => l.IsActive).HasColumnName("ACT_IND").IsRequired();

        builder.HasData(
            Row(ProductStatus.Draft, "DRAFT", "Draft", "Being prepared; not visible to shoppers"),
            Row(ProductStatus.Published, "PUBLISHED", "Published", "On sale"),
            Row(ProductStatus.Unpublished, "UNPUBLISHED", "Unpublished", "Taken off sale; kept in history"),
            Row(ProductStatus.Archived, "ARCHIVED", "Archived", "Retired for good; its address answers gone"));
    }

    private static object Row(ProductStatus status, string code, string name, string description) =>
        new { Id = (short)status, Code = code, Name = name, Description = (string?)description, SortSeq = (short)status, IsActive = true };
}

internal sealed class PlannedVariantConfiguration : IEntityTypeConfiguration<PlannedVariant>
{
    public void Configure(EntityTypeBuilder<PlannedVariant> builder)
    {
        builder.ToTable("PRDT_VRNT", table =>
        {
            // PRD-2, PRD-8, D4: a price is positive, and a compare-at price needs a price and is higher than it.
            table.HasCheckConstraint(
                "CK_PRDT_VRNT_PRIC",
                "(\"PRIC\" IS NULL OR \"PRIC\" > 0) AND (\"CMPR_PRIC\" IS NULL OR (\"PRIC\" IS NOT NULL AND \"CMPR_PRIC\" > \"PRIC\"))");
        });
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(v => v.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(v => v.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(v => v.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(v => v.Price).HasColumnName("PRIC").HasColumnType("numeric(12,2)");
        builder.Property(v => v.CompareAtPrice).HasColumnName("CMPR_PRIC").HasColumnType("numeric(12,2)");
        builder.HasIndex(v => new { v.ProductId, v.ColourId, v.SizeId }).IsUnique();
        builder.HasAuditColumns();
    }
}

internal sealed class PriceHistoryEntryConfiguration : IEntityTypeConfiguration<PriceHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PriceHistoryEntry> builder)
    {
        builder.ToTable("PRDT_PRIC_HIST");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(h => h.VariantId).HasColumnName("PRDT_VRNT_ID").IsRequired();
        builder.Property(h => h.OldPrice).HasColumnName("OLD_PRIC").HasColumnType("numeric(12,2)");
        builder.Property(h => h.NewPrice).HasColumnName("NEW_PRIC").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(h => h.OldCompareAtPrice).HasColumnName("OLD_CMPR_PRIC").HasColumnType("numeric(12,2)");
        builder.Property(h => h.NewCompareAtPrice).HasColumnName("NEW_CMPR_PRIC").HasColumnType("numeric(12,2)");
        builder.Property(h => h.ChangedBy).HasColumnName("CHNG_BY").HasMaxLength(100).IsRequired();
        builder.Property(h => h.InsrDte).HasColumnName("INSR_DTE").IsRequired();
        builder.Property(h => h.InsrBy).HasColumnName("INSR_BY").HasMaxLength(100).IsRequired();

        // Append-only (PRD-7): a database trigger refuses UPDATE and DELETE; the audit columns here are the insert pair only.
        builder.Ignore(h => h.UpdtDte);
        builder.Ignore(h => h.UpdtBy);
        builder.HasIndex(h => h.VariantId);
        builder.HasOne(h => h.Variant).WithMany().HasForeignKey(h => h.VariantId)
            .HasConstraintName("FK_PRDT_PRIC_HIST_PRDT_VRNT_ID").OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProductCollectionLinkConfiguration : IEntityTypeConfiguration<ProductCollectionLink>
{
    public void Configure(EntityTypeBuilder<ProductCollectionLink> builder)
    {
        builder.ToTable("PRDT_COLN_MAP");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(c => c.CollectionId).HasColumnName("COLN_ID").IsRequired();
        builder.HasIndex(c => new { c.ProductId, c.CollectionId }).IsUnique();
        builder.HasIndex(c => c.CollectionId);
        builder.HasAuditColumns();
    }
}

internal sealed class LabelledVariantConfiguration : IEntityTypeConfiguration<LabelledVariant>
{
    public void Configure(EntityTypeBuilder<LabelledVariant> builder)
    {
        builder.ToTable("VRNT_LBL");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.LabellingVariantId).HasColumnName("LBL_VRNT_ID").IsRequired();
        builder.Property(l => l.StyleId).HasColumnName("STYL_ID").IsRequired();
        builder.Property(l => l.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(l => l.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(l => l.SkuCode).HasColumnName("SKU_CODE").HasMaxLength(120).IsRequired();
        builder.Property(l => l.Gtin).HasColumnName("GTIN").HasMaxLength(13).IsRequired();
        builder.Property(l => l.GtinVoided).HasColumnName("GTIN_VOID_IND").IsRequired();

        // ADR 0014: written by upsert on the Labelling variant id, so a repeated or reordered event cannot add a second row.
        builder.HasIndex(l => l.LabellingVariantId).IsUnique();
        builder.HasIndex(l => new { l.StyleId, l.ColourId, l.SizeId });
        builder.HasAuditColumns();
    }
}
