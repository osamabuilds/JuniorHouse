namespace Romp.Modules.Storefront.Domain.StorefrontSettings;

/// <summary>The caps from the plan's "Size limits" (one constant each, Q-ST-9).</summary>
public static class SettingsLimits
{
    public const int MaxNavDepth = 3;
    public const int MaxNavItems = 100;
    public const int MaxLabelLength = 40;
    public const int MaxPages = 40;
    public const int MaxBlocksPerPage = 60;
    public const int MaxParagraphLength = 5000;
    public const int MaxMetaDescriptionLength = 160;
    public const int MaxPopularSearches = 12;
    public const int MaxSnapshotBytes = 512 * 1024;
}

/// <summary>What a navigation entry is. Maps to the <c>NAV_KIND_LKP</c> codes.</summary>
public enum NavKind
{
    Link,
    Group,
    CollectionList,
}

/// <summary>What a link points at: a typed reference, never a free URL. Maps to <c>LINK_TGT_LKP</c>.</summary>
public enum LinkTargetKind
{
    Path,
    Page,
    Collection,
    Category,
    External,
}

/// <summary>Maps to <c>PAGE_STS_LKP</c>.</summary>
public enum PageStatus
{
    Draft,
    Published,
    Archived,
}

/// <summary>Maps to <c>BLCK_TYP_LKP</c>.</summary>
public enum BlockType
{
    Heading,
    Paragraph,
    List,
    Table,
    Callout,
    Fact,
}

/// <summary>Maps to <c>STNG_KEY_LKP</c>.</summary>
public enum SettingKey
{
    BrandName,
    Tagline,
    Whatsapp,
    Email,
    Hours,
    Announcement,
    AnnouncementOn,
}

/// <summary>One problem found while validating the draft: the field it belongs to and what to do about it (ST-4).</summary>
public sealed record SettingsProblem(string Field, string Message);
