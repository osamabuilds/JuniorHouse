using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Storefront.Domain.ProductRecommendations;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Infrastructure.ProductRecommendations;

// Names follow docs/db/naming.md. PRDT_SMLR and PRDT_SALE_DAY are derived read tables (plan: data), so they hold ids and counts only.

internal sealed class SimilarProductConfiguration : IEntityTypeConfiguration<SimilarProduct>
{
    public void Configure(EntityTypeBuilder<SimilarProduct> builder)
    {
        builder.ToTable("PRDT_SMLR", table =>
        {
            table.HasCheckConstraint("CK_PRDT_SMLR_RANK_NO", "\"RANK_NO\" BETWEEN 1 AND 12");
            table.HasCheckConstraint("CK_PRDT_SMLR_DIFF_PRDT", "\"PRDT_ID\" <> \"SMLR_PRDT_ID\"");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(s => s.SimilarProductId).HasColumnName("SMLR_PRDT_ID").IsRequired();
        builder.Property(s => s.Rank).HasColumnName("RANK_NO").IsRequired();
        builder.Property(s => s.Score).HasColumnName("SCOR_VAL").HasPrecision(7, 6).IsRequired();
        builder.Property(s => s.CalculatedAt).HasColumnName("CALC_DTE").IsRequired();

        builder.HasIndex(s => new { s.ProductId, s.Rank }).IsUnique().HasDatabaseName("UQ_PRDT_SMLR_PRDT_ID_RANK_NO");
        builder.HasIndex(s => new { s.ProductId, s.SimilarProductId }).IsUnique().HasDatabaseName("UQ_PRDT_SMLR_PRDT_ID_SMLR_PRDT_ID");
        builder.HasIndex(s => s.SimilarProductId).HasDatabaseName("IX_PRDT_SMLR_SMLR_PRDT_ID");
        builder.HasOne<PublishedProduct>().WithMany().HasForeignKey(s => s.ProductId)
            .HasConstraintName("FK_PRDT_SMLR_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<PublishedProduct>().WithMany().HasForeignKey(s => s.SimilarProductId)
            .HasConstraintName("FK_PRDT_SMLR_SMLR_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class ProductSaleDayConfiguration : IEntityTypeConfiguration<ProductSaleDay>
{
    public void Configure(EntityTypeBuilder<ProductSaleDay> builder)
    {
        builder.ToTable("PRDT_SALE_DAY", table => table.HasCheckConstraint("CK_PRDT_SALE_DAY_UNIT_QTY", "\"UNIT_QTY\" >= 0"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(s => s.Day).HasColumnName("SALE_DTE").IsRequired();
        builder.Property(s => s.Units).HasColumnName("UNIT_QTY").IsRequired();

        builder.HasIndex(s => new { s.ProductId, s.Day }).IsUnique().HasDatabaseName("UQ_PRDT_SALE_DAY_PRDT_ID_SALE_DTE");
        builder.HasIndex(s => s.Day).HasDatabaseName("IX_PRDT_SALE_DAY_SALE_DTE");
        builder.HasOne<PublishedProduct>().WithMany().HasForeignKey(s => s.ProductId)
            .HasConstraintName("FK_PRDT_SALE_DAY_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class RecommendationRunConfiguration : IEntityTypeConfiguration<RecommendationRun>
{
    public void Configure(EntityTypeBuilder<RecommendationRun> builder)
    {
        builder.ToTable("RECM_RUN");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.StartedAt).HasColumnName("STRT_DTE").IsRequired();
        builder.Property(r => r.EndedAt).HasColumnName("END_DTE");
        builder.Property(r => r.GroupCount).HasColumnName("GRP_CNT").IsRequired();
        builder.Property(r => r.ProductCount).HasColumnName("PRDT_CNT").IsRequired();
        builder.Ignore(r => r.IsFinished);

        builder.HasIndex(r => r.EndedAt).HasDatabaseName("IX_RECM_RUN_END_DTE");
        builder.HasAuditColumns();
    }
}

internal sealed class RecommendationDayConfiguration : IEntityTypeConfiguration<RecommendationDay>
{
    public void Configure(EntityTypeBuilder<RecommendationDay> builder)
    {
        builder.ToTable("RECM_EVNT_DAY", table => table.HasCheckConstraint("CK_RECM_EVNT_DAY_CNTS", "\"CLK_CNT\" >= 0 AND \"ADD_CNT\" >= 0"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(d => d.Row).HasColumnName("ROW_ID").IsRequired();
        builder.Property(d => d.ProductId).HasColumnName("PRDT_ID").IsRequired();
        builder.Property(d => d.Day).HasColumnName("EVNT_DTE").IsRequired();
        builder.Property(d => d.ClickCount).HasColumnName("CLK_CNT").IsRequired();
        builder.Property(d => d.AddCount).HasColumnName("ADD_CNT").IsRequired();

        builder.HasIndex(d => new { d.Row, d.ProductId, d.Day }).IsUnique().HasDatabaseName("UQ_RECM_EVNT_DAY_ROW_ID_PRDT_ID_EVNT_DTE");
        builder.HasIndex(d => d.ProductId).HasDatabaseName("IX_RECM_EVNT_DAY_PRDT_ID");
        builder.HasOne<RecommendationRowLookup>().WithMany().HasForeignKey(d => d.Row)
            .HasConstraintName("FK_RECM_EVNT_DAY_RECM_ROW_LKP").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PublishedProduct>().WithMany().HasForeignKey(d => d.ProductId)
            .HasConstraintName("FK_RECM_EVNT_DAY_PRDT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}
