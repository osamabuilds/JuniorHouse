using Romp.Modules.Storefront.Domain.StorefrontSettings;

namespace Romp.Modules.Storefront.Tests.StorefrontSettings;

/// <summary>A small valid draft the specs change one thing in.</summary>
internal static class SettingsContentBuilder
{
    public static SettingsContent Valid(
        IReadOnlyList<NavItem>? navigation = null,
        IReadOnlyList<Page>? pages = null,
        IReadOnlyList<SettingItem>? items = null,
        IReadOnlyList<DeliveryRule>? delivery = null,
        ExchangePolicy? exchange = null,
        IReadOnlyList<FooterColumn>? footer = null,
        IReadOnlyList<AgeStep>? ages = null) =>
        new(
            navigation ?? [Link("girls", "Girls", "/girls", 1), Link("boys", "Boys", "/boys", 2)],
            ages ?? [new AgeStep("1y", "1 year", 1), new AgeStep("2y", "2 years", 2)],
            footer ?? [new FooterColumn("Help", 1, [new FooterLink("Delivery", new LinkTarget(LinkTargetKind.Path, "/help"), 1)])],
            items ?? [new SettingItem(SettingKey.BrandName, "Romp"), new SettingItem(SettingKey.Whatsapp, "+923315613217")],
            delivery ?? [new DeliveryRule("Lahore", 2, 3, 250m, 5000m, "TCS")],
            exchange ?? new ExchangePolicy(7, ["Unworn with tags."]),
            pages ?? [PublishedPage("delivery")],
            ["dresses"],
            ["instagram.com"]);

    public static NavItem Link(string key, string label, string path, int sort, bool enabled = true) =>
        new(key, label, NavKind.Link, new LinkTarget(LinkTargetKind.Path, path), sort, enabled);

    public static NavItem Group(string key, string label, int sort) => new(key, label, NavKind.Group, null, sort);

    public static Page PublishedPage(string slug, params PageBlock[] blocks)
    {
        var page = new Page(slug, "Title", "A short description.");
        page.ReplaceBlocks(blocks);
        page.SetStatus(PageStatus.Published);
        return page;
    }
}
