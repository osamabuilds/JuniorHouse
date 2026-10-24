using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

internal sealed class NavItemConfiguration : IEntityTypeConfiguration<NavItem>
{
    public void Configure(EntityTypeBuilder<NavItem> builder)
    {
        builder.ToTable("NAV_ITEM");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property<long?>("ParentId").HasColumnName("PRNT_ID");
        builder.Property(n => n.Key).HasColumnName("ITEM_KEY").HasMaxLength(40).IsRequired();
        builder.Property(n => n.Label).HasColumnName("LBL").HasMaxLength(40).IsRequired();
        builder.Property(n => n.Kind).HasColumnName("NAV_KIND_ID").IsRequired();
        builder.Property(n => n.TargetKind).HasColumnName("LINK_TGT_ID");
        builder.Property(n => n.TargetRef).HasColumnName("TGT_REF").HasMaxLength(300);
        builder.Property(n => n.SortSeq).HasColumnName("SORT_SEQ").HasColumnType("smallint").IsRequired();
        builder.Property(n => n.IsEnabled).HasColumnName("ACT_IND").IsRequired();
        builder.Property(n => n.ShowAges).HasColumnName("SHOW_AGE_IND").IsRequired();

        builder.HasMany(n => n.Children).WithOne().HasForeignKey("ParentId").HasConstraintName("FK_NAV_ITEM_PRNT_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<NavKindLookup>().WithMany().HasForeignKey(n => n.Kind).HasConstraintName("FK_NAV_ITEM_NAV_KIND_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LinkTargetLookup>().WithMany().HasForeignKey(n => n.TargetKind).HasConstraintName("FK_NAV_ITEM_LINK_TGT_ID").OnDelete(DeleteBehavior.Restrict);

        // Uniqueness of the key is the database's job. Sibling positions are checked by the validator instead of a unique index:
        // a reorder is saved row by row, and a unique (PRNT_ID, SORT_SEQ) would trip over the intermediate state of a swap.
        builder.HasIndex(n => n.Key).IsUnique();
        builder.HasIndex("ParentId", nameof(NavItem.SortSeq));
        builder.HasAuditColumns();
    }
}

internal sealed class AgeStepConfiguration : IEntityTypeConfiguration<AgeStep>
{
    public void Configure(EntityTypeBuilder<AgeStep> builder)
    {
        builder.ToTable("AGE_STEP");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(a => a.Code).HasColumnName("AGE_CODE").HasMaxLength(20).IsRequired();
        builder.Property(a => a.Label).HasColumnName("LBL").HasMaxLength(40).IsRequired();
        builder.Property(a => a.SortSeq).HasColumnName("SORT_SEQ").HasColumnType("smallint").IsRequired();
        builder.Property(a => a.IsEnabled).HasColumnName("ACT_IND").IsRequired();
        builder.HasIndex(a => a.Code).IsUnique();
        builder.HasAuditColumns();
    }
}

internal sealed class FooterColumnConfiguration : IEntityTypeConfiguration<FooterColumn>
{
    public void Configure(EntityTypeBuilder<FooterColumn> builder)
    {
        builder.ToTable("FOOT_COLM");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(c => c.Title).HasColumnName("TTL").HasMaxLength(40).IsRequired();
        builder.Property(c => c.SortSeq).HasColumnName("SORT_SEQ").HasColumnType("smallint").IsRequired();
        builder.HasMany(c => c.Links).WithOne().HasForeignKey("FooterColumnId").HasConstraintName("FK_FOOT_LINK_FOOT_COLM_ID").OnDelete(DeleteBehavior.Cascade);
        builder.HasAuditColumns();
    }
}

internal sealed class FooterLinkConfiguration : IEntityTypeConfiguration<FooterLink>
{
    public void Configure(EntityTypeBuilder<FooterLink> builder)
    {
        builder.ToTable("FOOT_LINK");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property<long>("FooterColumnId").HasColumnName("FOOT_COLM_ID");
        builder.Property(l => l.Label).HasColumnName("LBL").HasMaxLength(40).IsRequired();
        builder.Property(l => l.TargetKind).HasColumnName("LINK_TGT_ID").IsRequired();
        builder.Property(l => l.TargetRef).HasColumnName("TGT_REF").HasMaxLength(300).IsRequired();
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").HasColumnType("smallint").IsRequired();
        builder.Property(l => l.IsEnabled).HasColumnName("ACT_IND").IsRequired();
        builder.HasOne<LinkTargetLookup>().WithMany().HasForeignKey(l => l.TargetKind).HasConstraintName("FK_FOOT_LINK_LINK_TGT_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasAuditColumns();
    }
}

internal sealed class SettingItemConfiguration : IEntityTypeConfiguration<SettingItem>
{
    public void Configure(EntityTypeBuilder<SettingItem> builder)
    {
        builder.ToTable("STNG_ITEM");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(s => s.Key).HasColumnName("STNG_KEY_ID").IsRequired();
        builder.Property(s => s.Value).HasColumnName("VAL").HasMaxLength(500).IsRequired();
        builder.HasOne<SettingKeyLookup>().WithMany().HasForeignKey(s => s.Key).HasConstraintName("FK_STNG_ITEM_STNG_KEY_ID").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.Key).IsUnique();
        builder.HasAuditColumns();
    }
}

internal sealed class PopularSearchConfiguration : IEntityTypeConfiguration<PopularSearch>
{
    public void Configure(EntityTypeBuilder<PopularSearch> builder)
    {
        builder.ToTable("POPL_SRCH");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID").ValueGeneratedOnAdd();
        builder.Property(p => p.Text).HasColumnName("TEXT").HasMaxLength(40).IsRequired();
        builder.Property(p => p.SortSeq).HasColumnName("SORT_SEQ").HasColumnType("smallint").IsRequired();
        builder.HasIndex(p => p.Text).IsUnique();
        builder.HasAuditColumns();
    }
}
