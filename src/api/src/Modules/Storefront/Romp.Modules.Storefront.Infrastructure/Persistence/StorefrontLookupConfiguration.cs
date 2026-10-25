using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.Persistence;

/// <summary>
/// The system-owned Storefront lookups (docs/db/naming.md standard lookup shape), seeded by migration. The ids are the enum values in
/// the Domain, so a code never changes meaning. Growth is a new row, not a schema change.
/// </summary>
internal static class StorefrontLookupConfiguration
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        Configure<NavKindLookup, NavKind>(modelBuilder, "NAV_KIND_LKP",
            (NavKind.Link, "LINK", "Link", "Opens its target; may also have members"),
            (NavKind.Group, "GROUP", "Group", "Only opens its members"),
            (NavKind.CollectionList, "COLLECTION_LIST", "Collection list", "Lists the enabled collections as members"));

        Configure<LinkTargetLookup, LinkTargetKind>(modelBuilder, "LINK_TGT_LKP",
            (LinkTargetKind.Path, "PATH", "Path", "A path on the shop, for example /girls/dresses"),
            (LinkTargetKind.Page, "PAGE", "Page", "A published content page, by slug"),
            (LinkTargetKind.Collection, "COLLECTION", "Collection", "A catalogue collection, by code"),
            (LinkTargetKind.Category, "CATEGORY", "Category", "A catalogue category, by code"),
            (LinkTargetKind.External, "EXTERNAL", "External", "An https address on the allowed-sites list"));

        Configure<SettingKeyLookup, SettingKey>(modelBuilder, "STNG_KEY_LKP",
            (SettingKey.BrandName, "BRAND_NAME", "Brand name", null),
            (SettingKey.Tagline, "TAGLINE", "Tagline", null),
            (SettingKey.Whatsapp, "WHATSAPP", "WhatsApp number", "International format, for example +923001234567"),
            (SettingKey.Email, "EMAIL", "Email address", null),
            (SettingKey.Hours, "HOURS", "Opening hours", null),
            (SettingKey.Announcement, "ANNOUNCEMENT", "Announcement text", "Shown in the bar above the header"),
            (SettingKey.AnnouncementOn, "ANNOUNCEMENT_ON", "Announcement shown", "true or false"));

        Configure<PageStatusLookup, PageStatus>(modelBuilder, "PAGE_STS_LKP",
            (PageStatus.Draft, "DRAFT", "Draft", "Being written; not on the shop"),
            (PageStatus.Published, "PUBLISHED", "Published", "On the shop"),
            (PageStatus.Archived, "ARCHIVED", "Archived", "Removed; its address answers 410"));

        Configure<BlockTypeLookup, BlockType>(modelBuilder, "BLCK_TYP_LKP",
            (BlockType.Heading, "HEADING", "Heading", "Level 2 or 3"),
            (BlockType.Paragraph, "PARAGRAPH", "Paragraph", "Plain text"),
            (BlockType.List, "LIST", "List", "Ordered plain-text items"),
            (BlockType.Table, "TABLE", "Table", "Rows and columns of plain text"),
            (BlockType.Callout, "CALLOUT", "Callout", "A highlighted plain-text note"),
            (BlockType.Fact, "FACT", "Fact", "A value taken from the settings"));

        // Read model (published-catalog, Sprint 9a). The ids are the enum values in the Domain.
        Configure<DerivativeStatusLookup, DerivativeStatus>(modelBuilder, "DRV_STS_LKP",
            (DerivativeStatus.Pending, "PENDING", "Pending", "Waiting for the image worker"),
            (DerivativeStatus.Done, "DONE", "Done", "The file exists"),
            (DerivativeStatus.Failed, "FAILED", "Failed", "Gave up after the allowed attempts"));

        Configure<DerivativeFormatLookup, DerivativeFormat>(modelBuilder, "DRV_FMT_LKP",
            (DerivativeFormat.Webp, "WEBP", "WebP", "The format browsers prefer"),
            (DerivativeFormat.Jpeg, "JPG", "JPEG", "The fallback format"));

        Configure<RebuildStatusLookup, RebuildStatus>(modelBuilder, "RBLD_STS_LKP",
            (RebuildStatus.Running, "RUNNING", "Running", "Reading the Catalog and Warehouse contracts"),
            (RebuildStatus.Completed, "COMPLETED", "Completed", "Every row was written and the old ones swept"),
            (RebuildStatus.Failed, "FAILED", "Failed", "Stopped; the previous rows keep serving"));

        Configure<FactLookup, short>(modelBuilder, "FACT_LKP",
            SettingsFacts.Keys.Select((key, i) => ((short)(i + 1), key, key, (string?)"Quoted by fact blocks; empty facts block publishing")).ToArray());
    }

    private static void Configure<TLookup, TId>(ModelBuilder modelBuilder, string tableName, params (TId Id, string Code, string Name, string? Description)[] rows)
        where TLookup : StorefrontLookup<TId>
        where TId : notnull
    {
        var builder = ConfigureShape<TLookup, TId>(modelBuilder, tableName);
        var sort = (short)0;
        builder.HasData(rows.Select(r => (object)new
        {
            Id = r.Id, Code = r.Code, Name = r.Name, Description = r.Description, SortSeq = ++sort, IsActive = true,
        }).ToArray());
    }

    private static EntityTypeBuilder<TLookup> ConfigureShape<TLookup, TId>(ModelBuilder modelBuilder, string tableName)
        where TLookup : StorefrontLookup<TId>
        where TId : notnull
    {
        var builder = modelBuilder.Entity<TLookup>();
        builder.ToTable(tableName);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID").ValueGeneratedNever();
        builder.Property(l => l.Code).HasColumnName("CODE").HasMaxLength(30).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
        builder.Property(l => l.Name).HasColumnName("NAME").HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasColumnName("DSCR").HasMaxLength(500);
        builder.Property(l => l.SortSeq).HasColumnName("SORT_SEQ").IsRequired();
        builder.Property(l => l.IsActive).HasColumnName("ACT_IND").IsRequired();
        return builder;
    }
}
