using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Application.StorefrontSettings;

/// <summary>Maps between the admin DTOs and the domain entities. Entities are built fresh from a DTO (a set is replaced as a whole).</summary>
public static class SettingsMapper
{
    private static LinkTarget ToTarget(TargetDto dto) => new(dto.Kind, dto.Reference ?? string.Empty);

    public static NavItem ToEntity(NavItemDto dto)
    {
        var item = new NavItem(dto.Key, dto.Label, dto.Kind, dto.Target is null ? null : ToTarget(dto.Target), dto.Sort, dto.Enabled, dto.ShowAges);
        foreach (var child in dto.Children ?? [])
        {
            item.AddChild(ToEntity(child));
        }

        return item;
    }

    public static NavItemDto ToDto(NavItem item) => new(
        item.Key,
        item.Label,
        item.Kind,
        item.Target is null ? null : new TargetDto(item.Target.Kind, item.Target.Reference),
        item.SortSeq,
        item.IsEnabled,
        item.ShowAges,
        [.. item.Children.OrderBy(c => c.SortSeq).Select(ToDto)]);

    public static AgeStepDto ToDto(AgeStep step) => new(step.Code, step.Label, step.SortSeq, step.IsEnabled);

    public static FooterColumn ToEntity(FooterColumnDto dto) =>
        new(dto.Title, dto.Sort, (dto.Links ?? []).Select(l => new FooterLink(l.Label, ToTarget(l.Target), l.Sort, l.Enabled)));

    public static FooterColumnDto ToDto(FooterColumn column) => new(
        column.Title,
        column.SortSeq,
        [.. column.Links.OrderBy(l => l.SortSeq).Select(l => new FooterLinkDto(l.Label, new TargetDto(l.TargetKind, l.TargetRef), l.SortSeq, l.IsEnabled))]);

    public static DeliveryRule ToEntity(DeliveryRuleDto dto) =>
        new(dto.City, dto.MinDays, dto.MaxDays, dto.Charge, dto.FreeOver, dto.Courier, dto.Enabled);

    public static DeliveryRuleDto ToDto(DeliveryRule rule) =>
        new(rule.City, rule.MinDays, rule.MaxDays, rule.ChargeAmt, rule.FreeThresholdAmt, rule.Courier, rule.IsEnabled);

    public static ExchangeDto ToDto(ExchangePolicy policy) =>
        new(policy.WindowDays, [.. policy.Conditions.OrderBy(c => c.SortSeq).Select(c => c.Text)]);

    public static PageBlock ToEntity(PageBlockDto dto) => new(
        dto.Type,
        dto.Text ?? string.Empty,
        dto.Level,
        dto.FactKey,
        dto.Label,
        (dto.Items ?? []).Select(i => new PageBlockItem(i.Row, i.Column, i.Text)),
        dto.Ordered);

    public static PageBlockDto ToDto(PageBlock block) => new(
        block.Type,
        block.Text,
        block.Level,
        block.FactKey,
        block.Label,
        block.IsOrdered,
        [.. block.Items.OrderBy(i => i.RowNo).ThenBy(i => i.ColumnNo).Select(i => new PageBlockItemDto(i.RowNo, i.ColumnNo, i.Text))]);

    public static PageDto ToDto(Page page, uint rowVersion) => new(
        page.Id,
        page.Slug,
        page.Title,
        page.MetaDescription,
        page.Status,
        rowVersion,
        [.. page.Blocks.OrderBy(b => b.SortSeq).Select(ToDto)]);
}
