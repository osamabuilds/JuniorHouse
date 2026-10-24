using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

// Admin-facing shapes (DTOs, never entities). Money is decimal PKR; enums travel as their names.

public sealed record TargetDto(LinkTargetKind Kind, string Reference);

public sealed record NavItemDto(
    string Key,
    string Label,
    NavKind Kind,
    TargetDto? Target,
    int Sort,
    bool Enabled,
    bool ShowAges,
    IReadOnlyList<NavItemDto> Children);

public sealed record AgeStepDto(string Code, string Label, int Sort, bool Enabled);

public sealed record FooterLinkDto(string Label, TargetDto Target, int Sort, bool Enabled);

public sealed record FooterColumnDto(string Title, int Sort, IReadOnlyList<FooterLinkDto> Links);

public sealed record SettingItemDto(SettingKey Key, string Value);

public sealed record DeliveryRuleDto(string City, int MinDays, int MaxDays, decimal Charge, decimal? FreeOver, string Courier, bool Enabled);

public sealed record ExchangeDto(int WindowDays, IReadOnlyList<string> Conditions);

public sealed record PageBlockItemDto(int Row, int Column, string Text);

public sealed record PageBlockDto(
    BlockType Type,
    string Text,
    int Level,
    string? FactKey,
    string? Label,
    bool Ordered,
    IReadOnlyList<PageBlockItemDto> Items);

public sealed record PageDto(
    long Id,
    string Slug,
    string Title,
    string MetaDescription,
    PageStatus Status,
    uint RowVersion,
    IReadOnlyList<PageBlockDto> Blocks);

public sealed record PageRowDto(long Id, string Slug, string Title, PageStatus Status, int BlockCount);

/// <summary>The working copy of everything except page bodies. <see cref="Version"/> is the draft counter a save must quote (ST-14).</summary>
public sealed record DraftSettingsDto(
    int Version,
    IReadOnlyList<NavItemDto> Navigation,
    IReadOnlyList<AgeStepDto> Ages,
    IReadOnlyList<FooterColumnDto> Footer,
    IReadOnlyList<SettingItemDto> Items,
    IReadOnlyList<string> PopularSearches,
    IReadOnlyList<DeliveryRuleDto> Delivery,
    ExchangeDto? Exchange,
    IReadOnlyList<PageRowDto> Pages);

/// <summary>What a save returns: the new draft version and the part as stored.</summary>
public sealed record SavedDto<T>(int Version, T Data);

public sealed record SnapshotRowDto(int VersionNo, string DocumentHash, string Note, DateTimeOffset PublishedAt, string PublishedBy, bool IsCurrent);

public sealed record PublishResultDto(int VersionNo, string DocumentHash, bool Created);
