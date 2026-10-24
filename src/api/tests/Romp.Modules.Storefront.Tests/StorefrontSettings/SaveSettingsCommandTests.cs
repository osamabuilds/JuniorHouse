using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Application.StorefrontSettings;
using Romp.Modules.Storefront.Domain.StorefrontSettings;
using Romp.Modules.Storefront.Tests.Support;
using static Romp.Modules.Storefront.Tests.Support.TestData;

namespace Romp.Modules.Storefront.Tests.StorefrontSettings;

/// <summary>SCRUM-206 tasks 6 and 7: the save commands, against the seeded launch set on a real PostgreSQL.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SaveSettingsCommandTests(StorefrontPostgres postgres)
{
    private static async Task<string[]> FieldsOfAsync(Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(action);
        return [.. ex.Errors.Keys];
    }

    [Fact]
    [Trait("Spec", "ST-1")]
    public async Task SaveNavigation_ValidTree_IsStoredAndTheVersionMovesOn()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var version = await host.DraftVersionAsync();

        var saved = await host.SendAsync(new SaveNavigationCommand(version, [
            Link("girls", "Girls", "/girls", 1, Link("frocks", "Frocks", "/girls/frocks", 1, Link("party", "Party", "/girls/frocks/party", 1))),
            Link("sale", "Sale", "/sale", 2),
        ]));

        Assert.Equal(version + 1, saved.Version);
        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        Assert.Equal(["girls", "sale"], draft.Navigation.Select(n => n.Key).ToArray());
        Assert.Equal("party", draft.Navigation[0].Children[0].Children[0].Key);
        Assert.Equal(saved.Version, draft.Version);
    }

    [Fact]
    [Trait("Spec", "ST-1a")]
    [Trait("Spec", "ST-4")]
    public async Task SaveNavigation_FourthLevel_IsRejectedAndNothingIsSaved()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var version = await host.DraftVersionAsync();
        var before = await host.SendAsync(new GetDraftSettingsQuery());

        var ex = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SaveNavigationCommand(version, [
            Link("a", "A", "/a", 1, Link("bb", "B", "/b", 1, Link("cc", "C", "/c", 1, Link("dd", "D", "/d", 1)))),
        ])));

        Assert.Contains("Only 3 levels are allowed", string.Join(' ', ex.Errors.Values.SelectMany(v => v)));
        var after = await host.SendAsync(new GetDraftSettingsQuery());
        Assert.Equal(before.Navigation.Count, after.Navigation.Count);
        Assert.Equal(version, after.Version);
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public async Task SaveNavigation_EmptyGroupDuplicateKeyAndDeadPageLink_AreRejectedWithTheFieldName()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var version = await host.DraftVersionAsync();

        Assert.Contains("navigation.shop", await FieldsOfAsync(() => host.SendAsync(new SaveNavigationCommand(version, [Link("sale", "Sale", "/sale", 1), Group("shop", "Shop", 2)]))));
        Assert.Contains("navigation.sale", await FieldsOfAsync(() => host.SendAsync(new SaveNavigationCommand(version, [Link("sale", "Sale", "/sale", 1), Link("sale", "Sale 2", "/sale2", 2)]))));

        var dead = new NavItemDto("help", "Help", NavKind.Link, new TargetDto(LinkTargetKind.Page, "no-such-page"), 1, true, false, []);
        Assert.Contains("navigation.help", await FieldsOfAsync(() => host.SendAsync(new SaveNavigationCommand(version, [dead]))));

        var unsafeLink = new NavItemDto("x", "Evil", NavKind.Link, new TargetDto(LinkTargetKind.Path, "javascript:alert(1)"), 1, true, false, []);
        Assert.Contains("navigation.x", await FieldsOfAsync(() => host.SendAsync(new SaveNavigationCommand(version, [unsafeLink]))));
    }

    [Fact]
    [Trait("Spec", "ST-14")]
    public async Task SaveNavigation_WithAStaleVersion_IsRefused()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var version = await host.DraftVersionAsync();
        await host.SendAsync(new SaveNavigationCommand(version, [Link("sale", "Sale", "/sale", 1)]));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new SaveNavigationCommand(version, [Link("sale", "Sale again", "/sale", 1)])));

        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        Assert.Equal("Sale", draft.Navigation.Single().Label);
    }

    [Fact]
    [Trait("Spec", "ST-2")]
    public async Task SaveAgeSteps_RenamingALabelKeepsTheCode_AndAPublishedCodeCannotBeRemoved()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var draft = await host.SendAsync(new GetDraftSettingsQuery());

        var renamed = draft.Ages.Select(a => a.Code == "1y" ? a with { Label = "1 yr" } : a).ToList();
        var saved = await host.SendAsync(new SaveAgeStepsCommand(draft.Version, renamed));
        Assert.Equal("1 yr", saved.Data.Single(a => a.Code == "1y").Label);

        await host.PublishAllPagesAsync();
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "first"));

        var without1y = saved.Data.Where(a => a.Code != "1y").ToList();
        var fields = await FieldsOfAsync(async () => await host.SendAsync(new SaveAgeStepsCommand(await host.DraftVersionAsync(), without1y)));
        Assert.Contains("ages.1y", fields);

        var disabled = saved.Data.Select(a => a.Code == "1y" ? a with { Enabled = false } : a).ToList();
        await host.SendAsync(new SaveAgeStepsCommand(await host.DraftVersionAsync(), disabled));
    }

    [Theory]
    [Trait("Spec", "ST-4")]
    [InlineData("03315613217")]
    [InlineData("+92 331 5613217")]
    public async Task SaveContactSettings_BadPhoneNumber_IsRejectedWithAnExample(string number)
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var draft = await host.SendAsync(new GetDraftSettingsQuery());

        var ex = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new SaveContactSettingsCommand(
            draft.Version, [new SettingItemDto(SettingKey.Whatsapp, number)], draft.PopularSearches)));

        Assert.Contains("+923001234567", ex.Errors["settings.Whatsapp"].Single());
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public async Task SaveContactSettings_ValidValuesAndSearches_AreStoredInOrder()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var draft = await host.SendAsync(new GetDraftSettingsQuery());

        await host.SendAsync(new SaveContactSettingsCommand(
            draft.Version,
            [new SettingItemDto(SettingKey.BrandName, "Romp"), new SettingItemDto(SettingKey.Email, "hello@example.com"), new SettingItemDto(SettingKey.Whatsapp, "+923001234567")],
            ["Frocks", "Tees"]));

        var after = await host.SendAsync(new GetDraftSettingsQuery());
        Assert.Equal(["Frocks", "Tees"], after.PopularSearches.ToArray());
        Assert.Contains(after.Items, i => i is { Key: SettingKey.Email, Value: "hello@example.com" });
        Assert.Equal(3, after.Items.Count);
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public async Task SaveContactSettings_DuplicateKeyAndBadEmail_AreRejected()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var draft = await host.SendAsync(new GetDraftSettingsQuery());

        var fields = await FieldsOfAsync(() => host.SendAsync(new SaveContactSettingsCommand(
            draft.Version, [new SettingItemDto(SettingKey.Email, "nope"), new SettingItemDto(SettingKey.Email, "also-nope")], [])));

        Assert.Contains("settings.Email", fields);
    }

    [Fact]
    [Trait("Spec", "ST-7")]
    [Trait("Spec", "ST-4")]
    public async Task SaveDeliveryAndExchange_NegativeChargeAndDuplicateCity_AreRejected_AndValidRowsAreStored()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var draft = await host.SendAsync(new GetDraftSettingsQuery());

        var negative = new DeliveryRuleDto("Lahore", 2, 3, -1m, null, "TCS", true);
        Assert.Contains("delivery.Lahore", await FieldsOfAsync(() => host.SendAsync(new SaveDeliveryAndExchangeCommand(draft.Version, [negative], draft.Exchange))));
        var twice = new DeliveryRuleDto("Karachi", 3, 4, 300m, null, "TCS", true);
        Assert.Contains("delivery.Karachi", await FieldsOfAsync(() => host.SendAsync(new SaveDeliveryAndExchangeCommand(draft.Version, [twice, twice], draft.Exchange))));

        await host.SendAsync(new SaveDeliveryAndExchangeCommand(
            draft.Version, [new DeliveryRuleDto("Lahore", 1, 2, 199.50m, 4000m, "Leopards", true)], new ExchangeDto(10, ["Unworn.", "Tags on."])));

        var after = await host.SendAsync(new GetDraftSettingsQuery());
        var rule = Assert.Single(after.Delivery);
        Assert.Equal((199.50m, 4000m, "Leopards"), (rule.Charge, rule.FreeOver, rule.Courier));
        Assert.Equal(10, after.Exchange!.WindowDays);
        Assert.Equal(["Unworn.", "Tags on."], after.Exchange.Conditions.ToArray());
    }

    [Fact]
    [Trait("Spec", "ST-1")]
    public async Task SaveFooter_LinkToAnExistingDraftPage_IsAllowed_ButDangerousTargetsAreNot()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var draft = await host.SendAsync(new GetDraftSettingsQuery());

        var ok = new FooterColumnDto("Help", 1, [new FooterLinkDto("Delivery", new TargetDto(LinkTargetKind.Page, "delivery"), 1, true)]);
        await host.SendAsync(new SaveFooterCommand(draft.Version, [ok]));

        var evil = new FooterColumnDto("Help", 1, [new FooterLinkDto("Go", new TargetDto(LinkTargetKind.External, "https://evil.example/x"), 1, true)]);
        Assert.Contains("footer.Help.Go", await FieldsOfAsync(async () => await host.SendAsync(new SaveFooterCommand(await host.DraftVersionAsync(), [evil]))));

        var saved = await host.QueryAsync(c => c.FooterColumns.Include(f => f.Links).ToListAsync());
        Assert.Equal("Delivery", saved.Single().Links.Single().Label);
    }
}
