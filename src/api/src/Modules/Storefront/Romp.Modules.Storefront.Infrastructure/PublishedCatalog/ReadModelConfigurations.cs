using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Infrastructure.PublishedCatalog;

// Table and column names follow docs/db/naming.md (Sprint 9a glossary). Keys that come from the producers are never generated here
// (ValueGeneratedNever); child rows use identity keys. Read-model rows may denormalise (CLAUDE.md), the reason is in the plan.

internal sealed class PublishedProductConfiguration : IEntityTypeConfiguration<PublishedProduct>
{
    public void Configure(EntityTypeBuilder<PublishedProduct> builder)
    {
        builder.ToTable("PRDT_MAIN", table =>
        {
            table.HasCheckConstraint("CK_PRDT_MAIN_PRIC_ORDR", "\"MIN_PRIC\" IS NULL OR (\"MIN_PRIC\" > 0 AND \"MAX_PRIC\" >= \"MIN_PRIC\")");
            table.HasCheckConstraint("CK_PRDT_MAIN_DISC_PCT", "\"MAX_DISC_PCT\" >= 0 AND \"MAX_DISC_PCT\" < 100");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(p => p.ContentVersion).HasColumnName("CNTT_VER").IsRequired();
        builder.Property(p => p.Path).HasColumnName("PATH").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Name).HasColumnName("NAME").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasColumnName("DSCR").HasMaxLength(2000).IsRequired();
        builder.Property(p => p.StyleCode).HasColumnName("STYL_CODE").HasMaxLength(50).IsRequired();
        builder.Property(p => p.GenderCode).HasColumnName("GNDR_CODE").HasMaxLength(30).IsRequired();
        builder.Property(p => p.GenderName).HasColumnName("GNDR_NAME").HasMaxLength(100).IsRequired();
        builder.Property(p => p.CategoryCode).HasColumnName("CATG_CODE").HasMaxLength(30).IsRequired();
        builder.Property(p => p.CategoryName).HasColumnName("CATG_NAME").HasMaxLength(100).IsRequired();
        builder.Property(p => p.AgeCode).HasColumnName("AGE_CODE").HasMaxLength(30).IsRequired();
        builder.Property(p => p.AgeName).HasColumnName("AGE_NAME").HasMaxLength(100).IsRequired();
        builder.Property(p => p.PublishedAt).HasColumnName("PUBL_DTE").IsRequired();
        builder.Property(p => p.CareNote).HasColumnName("CARE_NOTE").HasMaxLength(500);
        builder.Property(p => p.MinPrice).HasColumnName("MIN_PRIC").HasColumnType("numeric(12,2)");
        builder.Property(p => p.MaxPrice).HasColumnName("MAX_PRIC").HasColumnType("numeric(12,2)");
        builder.Property(p => p.IsOnSale).HasColumnName("SALE_IND").IsRequired();
        builder.Property(p => p.MaxDiscountPercent).HasColumnName("MAX_DISC_PCT").HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(p => p.IsAvailable).HasColumnName("AVLB_IND").IsRequired();
        builder.Property(p => p.IsListable).HasColumnName("VSBL_IND").IsRequired();
        builder.Property(p => p.ImageReady).HasColumnName("IMG_RDY_IND").IsRequired();
        builder.Property(p => p.RebuildRunId).HasColumnName("RBLD_RUN_ID");

        builder.HasIndex(p => p.Path).IsUnique();
        // The listing reads (audience page, category page, newest first) and the "new in" / "sale" views.
        builder.HasIndex(p => new { p.GenderCode, p.CategoryCode, p.PublishedAt }).IsDescending(false, false, true);
        builder.HasIndex(p => new { p.IsListable, p.PublishedAt }).IsDescending(false, true);
        builder.HasIndex(p => p.MinPrice);
        builder.HasIndex(p => p.RebuildRunId);

        builder.HasOne<RebuildRun>().WithMany().HasForeignKey(p => p.RebuildRunId)
            .HasConstraintName("FK_PRDT_MAIN_RBLD_RUN_ID").OnDelete(DeleteBehavior.SetNull);
        builder.HasAuditColumns();
    }
}

internal sealed class VariantRowConfiguration : IEntityTypeConfiguration<VariantRow>
{
    public void Configure(EntityTypeBuilder<VariantRow> builder)
    {
        builder.ToTable("PRDT_VRNT", table =>
        {
            table.HasCheckConstraint("CK_PRDT_VRNT_PRIC_POS", "\"PRIC\" IS NULL OR \"PRIC\" > 0");
            table.HasCheckConstraint("CK_PRDT_VRNT_CMPR_PRIC", "\"CMPR_PRIC\" IS NULL OR (\"PRIC\" IS NOT NULL AND \"CMPR_PRIC\" > \"PRIC\")");
            table.HasCheckConstraint("CK_PRDT_VRNT_AVLB_QTY", "\"AVLB_QTY\" IS NULL OR \"AVLB_QTY\" >= 0");
        });
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(v => v.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(v => v.ColourId).HasColumnName("CLR_ID").IsRequired();
        builder.Property(v => v.ColourCode).HasColumnName("CLR_CODE").HasMaxLength(30).IsRequired();
        builder.Property(v => v.ColourName).HasColumnName("CLR_NAME").HasMaxLength(100).IsRequired();
        builder.Property(v => v.SizeId).HasColumnName("SIZE_ID").IsRequired();
        builder.Property(v => v.SizeCode).HasColumnName("SIZE_CODE").HasMaxLength(30).IsRequired();
        builder.Property(v => v.SizeName).HasColumnName("SIZE_NAME").HasMaxLength(100).IsRequired();
        builder.Property(v => v.SizeSeq).HasColumnName("SIZE_SEQ").IsRequired();
        builder.Property(v => v.SkuCode).HasColumnName("SKU_CODE").HasMaxLength(120);
        builder.Property(v => v.SkuNormalised).HasColumnName("SKU_NORM").HasMaxLength(120);
        builder.Property(v => v.Price).HasColumnName("PRIC").HasColumnType("numeric(12,2)");
        builder.Property(v => v.CompareAtPrice).HasColumnName("CMPR_PRIC").HasColumnType("numeric(12,2)");
        builder.Property(v => v.IsSellable).HasColumnName("SELL_IND").IsRequired();
        builder.Property(v => v.AvailableQuantity).HasColumnName("AVLB_QTY");
        builder.Property(v => v.StockVersion).HasColumnName("STCK_VER").IsRequired().HasDefaultValue(0L);

        builder.HasIndex(v => v.ProductId);
        // Exact SKU search (FR-06): a SKU belongs to one variant.
        builder.HasIndex(v => v.SkuNormalised).IsUnique().HasFilter("\"SKU_NORM\" IS NOT NULL");
        // The same-variant size/colour filter (Q-LST-9) only ever asks about variants that have stock.
        builder.HasIndex(v => new { v.SizeCode, v.ColourCode, v.ProductId }).HasFilter("\"AVLB_QTY\" > 0");

        builder.HasOne<PublishedProduct>().WithMany().HasForeignKey(v => v.ProductId)
            .HasConstraintName("FK_PRDT_VRNT_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class ImageRowConfiguration : IEntityTypeConfiguration<ImageRow>
{
    public void Configure(EntityTypeBuilder<ImageRow> builder)
    {
        builder.ToTable("PRDT_IMG", table => table.HasCheckConstraint("CK_PRDT_IMG_SIZE_POS", "\"WDTH\" > 0 AND \"HGHT\" > 0"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(i => i.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(i => i.ColourId).HasColumnName("CLR_ID");
        builder.Property(i => i.FileKey).HasColumnName("FILE_KEY").HasMaxLength(200).IsRequired();
        builder.Property(i => i.ContentHash).HasColumnName("HASH").HasMaxLength(32).IsFixedLength();
        builder.Property(i => i.Width).HasColumnName("WDTH").IsRequired();
        builder.Property(i => i.Height).HasColumnName("HGHT").IsRequired();
        builder.Property(i => i.AltText).HasColumnName("ALT").HasMaxLength(125).IsRequired();
        builder.Property(i => i.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(i => i.IsMain).HasColumnName("MAIN_IND").IsRequired();

        builder.HasIndex(i => new { i.ProductId, i.SortSeq });

        builder.HasOne<PublishedProduct>().WithMany().HasForeignKey(i => i.ProductId)
            .HasConstraintName("FK_PRDT_IMG_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class DerivativeRowConfiguration : IEntityTypeConfiguration<DerivativeRow>
{
    public void Configure(EntityTypeBuilder<DerivativeRow> builder)
    {
        builder.ToTable("IMG_DRV", table =>
        {
            table.HasCheckConstraint("CK_IMG_DRV_WDTH_POS", "\"WDTH\" > 0");
            table.HasCheckConstraint("CK_IMG_DRV_ATMP_QTY", "\"ATMP_QTY\" >= 0");
        });
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(d => d.ImageId).HasColumnName("IMG_ID").IsRequired();
        builder.Property(d => d.Width).HasColumnName("WDTH").IsRequired();
        builder.Property(d => d.Format).HasColumnName("DRV_FMT_ID").IsRequired();
        builder.Property(d => d.Status).HasColumnName("DRV_STS_ID").IsRequired();
        builder.Property(d => d.FileKey).HasColumnName("FILE_KEY").HasMaxLength(80);
        builder.Property(d => d.AttemptCount).HasColumnName("ATMP_QTY").IsRequired().HasDefaultValue(0);
        builder.Property(d => d.NextAttemptAt).HasColumnName("NXT_ATMP_DTE");
        builder.Property(d => d.ErrorCode).HasColumnName("ERR_CODE").HasMaxLength(40);

        // One row per image, width and format; the worker claims the oldest due pending rows (SKIP LOCKED).
        builder.HasIndex(d => new { d.ImageId, d.Width, d.Format }).IsUnique();
        builder.HasIndex(d => new { d.Status, d.NextAttemptAt });
        builder.HasIndex(d => d.Format);

        builder.HasOne<ImageRow>().WithMany().HasForeignKey(d => d.ImageId)
            .HasConstraintName("FK_IMG_DRV_IMG_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DerivativeStatusLookup>().WithMany().HasForeignKey(d => d.Status)
            .HasConstraintName("FK_IMG_DRV_DRV_STS_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DerivativeFormatLookup>().WithMany().HasForeignKey(d => d.Format)
            .HasConstraintName("FK_IMG_DRV_DRV_FMT_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasAuditColumns();
    }
}

internal sealed class CompositionRowConfiguration : IEntityTypeConfiguration<CompositionRow>
{
    public void Configure(EntityTypeBuilder<CompositionRow> builder)
    {
        builder.ToTable("PRDT_CMPS", table => table.HasCheckConstraint("CK_PRDT_CMPS_PCT_RNG", "\"PCT\" > 0 AND \"PCT\" <= 100"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(c => c.FibreCode).HasColumnName("FIBR_CODE").HasMaxLength(30).IsRequired();
        builder.Property(c => c.FibreName).HasColumnName("FIBR_NAME").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Percent).HasColumnName("PCT").HasColumnType("numeric(4,1)").IsRequired();
        builder.Property(c => c.SortSeq).HasColumnName("SORT_SEQ").IsRequired();

        builder.HasIndex(c => new { c.ProductId, c.FibreCode }).IsUnique();

        builder.HasOne<PublishedProduct>().WithMany().HasForeignKey(c => c.ProductId)
            .HasConstraintName("FK_PRDT_CMPS_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class CareRowConfiguration : IEntityTypeConfiguration<CareRow>
{
    public void Configure(EntityTypeBuilder<CareRow> builder)
    {
        builder.ToTable("PRDT_CARE");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(c => c.CareCode).HasColumnName("CARE_CODE").HasMaxLength(30).IsRequired();
        builder.Property(c => c.CareName).HasColumnName("CARE_NAME").HasMaxLength(100).IsRequired();
        builder.Property(c => c.SymbolCode).HasColumnName("SYMB_CODE").HasMaxLength(30).IsRequired();
        builder.Property(c => c.SortSeq).HasColumnName("SORT_SEQ").IsRequired();

        builder.HasIndex(c => new { c.ProductId, c.CareCode }).IsUnique();

        builder.HasOne<PublishedProduct>().WithMany().HasForeignKey(c => c.ProductId)
            .HasConstraintName("FK_PRDT_CARE_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class CollectionRowConfiguration : IEntityTypeConfiguration<CollectionRow>
{
    public void Configure(EntityTypeBuilder<CollectionRow> builder)
    {
        builder.ToTable("PRDT_COLN_MAP");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(c => c.CollectionCode).HasColumnName("COLN_CODE").HasMaxLength(30).IsRequired();
        builder.Property(c => c.CollectionName).HasColumnName("COLN_NAME").HasMaxLength(100).IsRequired();

        builder.HasIndex(c => new { c.ProductId, c.CollectionCode }).IsUnique();
        // "Which products are in this collection" is the collection listing.
        builder.HasIndex(c => c.CollectionCode);

        builder.HasOne<PublishedProduct>().WithMany().HasForeignKey(c => c.ProductId)
            .HasConstraintName("FK_PRDT_COLN_MAP_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class SizeChartRowConfiguration : IEntityTypeConfiguration<SizeChartRow>
{
    public void Configure(EntityTypeBuilder<SizeChartRow> builder)
    {
        builder.ToTable("CHRT_MAIN");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(c => c.ContentVersion).HasColumnName("CNTT_VER").IsRequired();
        builder.Property(c => c.CategoryCode).HasColumnName("CATG_CODE").HasMaxLength(30).IsRequired();
        builder.Property(c => c.CategoryName).HasColumnName("CATG_NAME").HasMaxLength(100).IsRequired();
        builder.Property(c => c.GenderCode).HasColumnName("GNDR_CODE").HasMaxLength(30).IsRequired();
        builder.Property(c => c.GenderName).HasColumnName("GNDR_NAME").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();

        // CHT-5: one chart per category and gender; a product asks for its own, then the Unisex one.
        builder.HasIndex(c => new { c.CategoryCode, c.GenderCode }).IsUnique();
        builder.HasAuditColumns();
    }
}

internal sealed class SizeChartLineRowConfiguration : IEntityTypeConfiguration<SizeChartLineRow>
{
    public void Configure(EntityTypeBuilder<SizeChartLineRow> builder)
    {
        builder.ToTable("CHRT_LINE");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(l => l.ChartId).HasColumnName("CHRT_ID").IsRequired();
        builder.Property(l => l.SizeCode).HasColumnName("SIZE_CODE").HasMaxLength(30).IsRequired();
        builder.Property(l => l.SizeName).HasColumnName("SIZE_NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.SizeSeq).HasColumnName("SIZE_SEQ").IsRequired();
        builder.Property(l => l.AgeMinMonths).HasColumnName("AGE_MIN_MON");
        builder.Property(l => l.AgeMaxMonths).HasColumnName("AGE_MAX_MON");
        builder.Property(l => l.HeightMinCm).HasColumnName("HGHT_MIN_CM").HasColumnType("numeric(6,2)");
        builder.Property(l => l.HeightMaxCm).HasColumnName("HGHT_MAX_CM").HasColumnType("numeric(6,2)");
        builder.Property(l => l.WeightMinKg).HasColumnName("WGHT_MIN_KG").HasColumnType("numeric(6,2)");
        builder.Property(l => l.WeightMaxKg).HasColumnName("WGHT_MAX_KG").HasColumnType("numeric(6,2)");
        builder.Property(l => l.ChestCm).HasColumnName("CHST_CM").HasColumnType("numeric(6,2)");
        builder.Property(l => l.WaistCm).HasColumnName("WAIS_CM").HasColumnType("numeric(6,2)");
        builder.Property(l => l.LengthCm).HasColumnName("LEN_CM").HasColumnType("numeric(6,2)");

        builder.HasIndex(l => new { l.ChartId, l.SizeCode }).IsUnique();

        builder.HasOne<SizeChartRow>().WithMany().HasForeignKey(l => l.ChartId)
            .HasConstraintName("FK_CHRT_LINE_CHRT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class StockRowConfiguration : IEntityTypeConfiguration<StockRow>
{
    public void Configure(EntityTypeBuilder<StockRow> builder)
    {
        builder.ToTable("AVLB_STK", table => table.HasCheckConstraint("CK_AVLB_STK_QTY_NONNEG", "\"QTY\" >= 0"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(s => s.SkuCode).HasColumnName("SKU_CODE").HasMaxLength(120).IsRequired();
        builder.Property(s => s.Quantity).HasColumnName("QTY").IsRequired();
        builder.Property(s => s.StockVersion).HasColumnName("STCK_VER").IsRequired();

        // The join to a variant is by SKU (Catalog and Warehouse use different variant ids).
        builder.HasIndex(s => s.SkuCode).IsUnique();
        builder.HasAuditColumns();
    }
}

internal sealed class RebuildRunConfiguration : IEntityTypeConfiguration<RebuildRun>
{
    public void Configure(EntityTypeBuilder<RebuildRun> builder)
    {
        builder.ToTable("RBLD_RUN", table =>
        {
            table.HasCheckConstraint("CK_RBLD_RUN_CNT_NONNEG", "\"PRDT_CNT\" >= 0 AND \"CHRT_CNT\" >= 0 AND \"STCK_CNT\" >= 0");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.Status).HasColumnName("RBLD_STS_ID").IsRequired();
        builder.Property(r => r.StartedAt).HasColumnName("STRT_DTE").IsRequired();
        builder.Property(r => r.EndedAt).HasColumnName("END_DTE");
        builder.Property(r => r.ProductCount).HasColumnName("PRDT_CNT").IsRequired().HasDefaultValue(0);
        builder.Property(r => r.ChartCount).HasColumnName("CHRT_CNT").IsRequired().HasDefaultValue(0);
        builder.Property(r => r.StockCount).HasColumnName("STCK_CNT").IsRequired().HasDefaultValue(0);
        builder.Property(r => r.Actor).HasColumnName("ACTR").HasMaxLength(100).IsRequired();

        builder.HasIndex(r => new { r.Status, r.StartedAt });

        builder.HasOne<RebuildStatusLookup>().WithMany().HasForeignKey(r => r.Status)
            .HasConstraintName("FK_RBLD_RUN_RBLD_STS_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasAuditColumns();
    }
}

internal sealed class ModelMetaConfiguration : IEntityTypeConfiguration<ModelMeta>
{
    public void Configure(EntityTypeBuilder<ModelMeta> builder)
    {
        builder.ToTable("MODL_META", table => table.HasCheckConstraint("CK_MODL_META_ONLY_ROW", $"\"ID\" = {ModelMeta.OnlyId}"));
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(m => m.ModelVersion).HasColumnName("MODL_VER").IsRequired();
        builder.Property(m => m.LookupsUpdatedAt).HasColumnName("LKP_UPDT_DTE");
        builder.HasAuditColumns();

        // Version 0 = never built, so the first start runs a rebuild (PCT-6).
        builder.HasData(new
        {
            Id = ModelMeta.OnlyId,
            ModelVersion = 0,
            InsrDte = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero),
            InsrBy = "migration",
        });
    }
}
