using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

internal sealed class DeliveryRuleConfiguration : IEntityTypeConfiguration<DeliveryRule>
{
    public void Configure(EntityTypeBuilder<DeliveryRule> builder)
    {
        builder.ToTable("DLVR_RULE");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.City).HasColumnName("CITY").HasMaxLength(60).IsRequired();
        builder.Property(r => r.MinDays).HasColumnName("MIN_DAY").HasColumnType("smallint").IsRequired();
        builder.Property(r => r.MaxDays).HasColumnName("MAX_DAY").HasColumnType("smallint").IsRequired();
        builder.Property(r => r.ChargeAmt).HasColumnName("CHRG_AMT").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(r => r.FreeThresholdAmt).HasColumnName("FREE_THRS_AMT").HasColumnType("numeric(12,2)");
        builder.Property(r => r.Courier).HasColumnName("CRER").HasMaxLength(60).IsRequired();
        builder.Property(r => r.IsEnabled).HasColumnName("ACT_IND").IsRequired();

        // One row per city; the row is the city's entry in the delivery table.
        builder.HasIndex(r => r.City).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("CK_DLVR_RULE_DAY_ORDR", "\"MIN_DAY\" >= 0 AND \"MAX_DAY\" >= \"MIN_DAY\""));
        builder.ToTable(t => t.HasCheckConstraint("CK_DLVR_RULE_AMT_POS", "\"CHRG_AMT\" >= 0 AND (\"FREE_THRS_AMT\" IS NULL OR \"FREE_THRS_AMT\" >= 0)"));
        builder.HasAuditColumns();
    }
}

internal sealed class ExchangePolicyConfiguration : IEntityTypeConfiguration<ExchangePolicy>
{
    public void Configure(EntityTypeBuilder<ExchangePolicy> builder)
    {
        builder.ToTable("XCHG_POLY");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.WindowDays).HasColumnName("WNDW_DAY").HasColumnType("smallint").IsRequired();
        builder.HasMany(p => p.Conditions).WithOne().HasForeignKey("PolicyId").HasConstraintName("FK_XCHG_COND_XCHG_POLY_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class ExchangeConditionConfiguration : IEntityTypeConfiguration<ExchangeCondition>
{
    public void Configure(EntityTypeBuilder<ExchangeCondition> builder)
    {
        builder.ToTable("XCHG_COND");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property<long>("PolicyId").HasColumnName("XCHG_POLY_ID");
        builder.Property(c => c.Text).HasColumnName("TEXT").HasMaxLength(300).IsRequired();
        builder.Property(c => c.SortSeq).HasColumnName("SORT_SEQ").HasColumnType("smallint").IsRequired();
        builder.HasAuditColumns();
    }
}

internal sealed class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("PAGE_MAIN");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.Slug).HasColumnName("SLUG").HasMaxLength(60).IsRequired();
        builder.Property(p => p.Title).HasColumnName("TTL").HasMaxLength(120).IsRequired();
        builder.Property(p => p.MetaDescription).HasColumnName("META_DSCR").HasMaxLength(160).IsRequired();
        builder.Property(p => p.Status).HasColumnName("PAGE_STS_ID").IsRequired();

        builder.HasOne<PageStatusLookup>().WithMany().HasForeignKey(p => p.Status).HasConstraintName("FK_PAGE_MAIN_PAGE_STS_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Blocks).WithOne().HasForeignKey("PageId").HasConstraintName("FK_PAGE_BLCK_PAGE_MAIN_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasAuditColumns();
    }
}

internal sealed class PageBlockConfiguration : IEntityTypeConfiguration<PageBlock>
{
    public void Configure(EntityTypeBuilder<PageBlock> builder)
    {
        builder.ToTable("PAGE_BLCK");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property<long>("PageId").HasColumnName("PAGE_MAIN_ID");
        builder.Property(b => b.SortSeq).HasColumnName("SORT_SEQ").HasColumnType("smallint").IsRequired();
        builder.Property(b => b.Type).HasColumnName("BLCK_TYP_ID").IsRequired();
        builder.Property(b => b.Text).HasColumnName("TEXT").HasMaxLength(5000).IsRequired();
        builder.Property(b => b.Level).HasColumnName("LVL").HasColumnType("smallint").IsRequired();
        builder.Property(b => b.FactId).HasColumnName("FACT_ID");
        builder.Property(b => b.Label).HasColumnName("LBL").HasMaxLength(120);
        builder.Property(b => b.IsOrdered).HasColumnName("ORDR_IND").IsRequired();

        builder.HasOne<BlockTypeLookup>().WithMany().HasForeignKey(b => b.Type).HasConstraintName("FK_PAGE_BLCK_BLCK_TYP_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FactLookup>().WithMany().HasForeignKey(b => b.FactId).HasConstraintName("FK_PAGE_BLCK_FACT_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(b => b.Items).WithOne().HasForeignKey("BlockId").HasConstraintName("FK_PAGE_BLCK_ITM_PAGE_BLCK_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class PageBlockItemConfiguration : IEntityTypeConfiguration<PageBlockItem>
{
    public void Configure(EntityTypeBuilder<PageBlockItem> builder)
    {
        builder.ToTable("PAGE_BLCK_ITM");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property<long>("BlockId").HasColumnName("PAGE_BLCK_ID");
        builder.Property(i => i.RowNo).HasColumnName("ROW_NO").HasColumnType("smallint").IsRequired();
        builder.Property(i => i.ColumnNo).HasColumnName("COLM_NO").HasColumnType("smallint").IsRequired();
        builder.Property(i => i.Text).HasColumnName("TEXT").HasMaxLength(500).IsRequired();
        builder.HasAuditColumns();
    }
}

internal sealed class PageRedirectConfiguration : IEntityTypeConfiguration<PageRedirect>
{
    public void Configure(EntityTypeBuilder<PageRedirect> builder)
    {
        builder.ToTable("PAGE_RDIR");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(r => r.OldSlug).HasColumnName("OLD_SLUG").HasMaxLength(60).IsRequired();
        builder.Property(r => r.PageId).HasColumnName("PAGE_MAIN_ID").IsRequired();
        builder.HasOne<Page>().WithMany().HasForeignKey(r => r.PageId).HasConstraintName("FK_PAGE_RDIR_PAGE_MAIN_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => r.OldSlug).IsUnique();
        builder.HasAuditColumns();
    }
}

internal sealed class SettingsDraftConfiguration : IEntityTypeConfiguration<SettingsDraft>
{
    public void Configure(EntityTypeBuilder<SettingsDraft> builder)
    {
        builder.ToTable("DRFT_MAIN");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(d => d.Version).HasColumnName("VER_NO").IsRequired();
        builder.HasAuditColumns();
    }
}

internal sealed class PublishedSnapshotConfiguration : IEntityTypeConfiguration<PublishedSnapshot>
{
    public void Configure(EntityTypeBuilder<PublishedSnapshot> builder)
    {
        builder.ToTable("PUBL_SNAP");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.VersionNo).HasColumnName("VER_NO").IsRequired();
        builder.Property(s => s.Document).HasColumnName("DOC").HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.DocumentHash).HasColumnName("DOC_HASH").HasColumnType("char(64)").IsRequired();
        builder.Property(s => s.IsCurrent).HasColumnName("CRNT_IND").IsRequired();
        builder.Property(s => s.Note).HasColumnName("NOTE").HasMaxLength(500).IsRequired();

        builder.HasIndex(s => s.VersionNo).IsUnique();

        // At most one current snapshot, decided by the database (ADR 0020).
        builder.HasIndex(s => s.IsCurrent).IsUnique().HasFilter("\"CRNT_IND\"").HasDatabaseName("UQ_PUBL_SNAP_CRNT_IND");
        builder.HasAuditColumns();
    }
}
