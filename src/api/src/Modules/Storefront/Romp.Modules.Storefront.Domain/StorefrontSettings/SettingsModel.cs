using System.Text.Json.Serialization;

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

// The numeric values below are the lookup-row IDs (smallint) seeded by the migration, so they never change or get reused.

/// <summary>What a navigation entry is. Maps to the <c>NAV_KIND_LKP</c> rows.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NavKind : short
{
    Link = 1,
    Group = 2,
    CollectionList = 3,
}

/// <summary>What a link points at: a typed reference, never a free URL. Maps to <c>LINK_TGT_LKP</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LinkTargetKind : short
{
    Path = 1,
    Page = 2,
    Collection = 3,
    Category = 4,
    External = 5,
}

/// <summary>Maps to <c>PAGE_STS_LKP</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PageStatus : short
{
    Draft = 1,
    Published = 2,
    Archived = 3,
}

/// <summary>Maps to <c>BLCK_TYP_LKP</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BlockType : short
{
    Heading = 1,
    Paragraph = 2,
    List = 3,
    Table = 4,
    Callout = 5,
    Fact = 6,
}

/// <summary>Maps to <c>STNG_KEY_LKP</c>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SettingKey : short
{
    BrandName = 1,
    Tagline = 2,
    Whatsapp = 3,
    Email = 4,
    Hours = 5,
    Announcement = 6,
    AnnouncementOn = 7,
}

/// <summary>One problem found while validating the draft: the field it belongs to and what to do about it (ST-4).</summary>
public sealed record SettingsProblem(string Field, string Message);
