using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Application.StorefrontSettings;
using Romp.Modules.Storefront.Domain.StorefrontSettings;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;
using Romp.Modules.Storefront.Tests.Support;
using static Romp.Modules.Storefront.Tests.Support.TestData;

namespace Romp.Modules.Storefront.Tests.StorefrontSettings;

/// <summary>SCRUM-206 tasks 9 to 11: publish, restore, snapshots and the public queries.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class PublishTests(StorefrontPostgres postgres)
{
    private static async Task<StorefrontHost> PublishableAsync(StorefrontPostgres postgres)
    {
        var host = await StorefrontHost.CreateAsync(postgres);
        await host.PublishAllPagesAsync();
        return host;
    }

    private static JsonNode Settings(PublishedSettingsDto? dto) => JsonNode.Parse(dto!.Json)!;

    [Fact]
    [Trait("Spec", "ST-3")]
    public async Task Publish_ValidDraft_MakesOneSnapshotCurrentAndWritesTheEvent()
    {
        using var host = await PublishableAsync(postgres);

        var result = await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "launch"));

        Assert.Equal((1, true), (result.VersionNo, result.Created));
        Assert.Equal(64, result.DocumentHash.Length);
        var snapshots = await host.QueryAsync(c => c.Snapshots.ToListAsync());
        Assert.Equal([1], snapshots.Where(s => s.IsCurrent).Select(s => s.VersionNo).ToArray());
        var events = await host.QueryAsync(async c => await c.Database.SqlQueryRaw<long>("""SELECT COUNT(*) AS "Value" FROM "STFR"."OUTB_MSG" """).SingleAsync());
        Assert.Equal(1, events);
    }

    [Fact]
    [Trait("Spec", "ST-3")]
    public async Task Publish_SameContentAgain_ReturnsTheExistingSnapshot()
    {
        using var host = await PublishableAsync(postgres);
        var first = await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "one"));

        var second = await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "two"));

        Assert.Equal((first.VersionNo, first.DocumentHash, false), (second.VersionNo, second.DocumentHash, second.Created));
        Assert.Single(await host.QueryAsync(c => c.Snapshots.ToListAsync()));
    }

    [Fact]
    [Trait("Spec", "ST-3")]
    public async Task Publish_ADraftEditAfterwards_IsNotVisibleUntilItIsPublished()
    {
        using var host = await PublishableAsync(postgres);
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "one"));

        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        await host.SendAsync(new SaveNavigationCommand(draft.Version, [Link("sale", "Final sale", "/sale", 1)]));

        Assert.Equal("Girls", Settings(await host.SendAsync(new GetPublishedSettingsQuery()))["navigation"]![0]!["label"]!.GetValue<string>());

        host.Cache.Invalidate();
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "two"));
        host.Cache.Invalidate();
        var published = await host.SendAsync(new GetPublishedSettingsQuery());
        Assert.Equal(2, published!.VersionNo);
        Assert.Equal("Final sale", Settings(published)["navigation"]![0]!["label"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Spec", "ST-17")]
    public async Task Publish_APublishedPageQuotingAnEmptyFact_IsBlocked()
    {
        using var host = await PublishableAsync(postgres);
        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        await host.SendAsync(new SaveDeliveryAndExchangeCommand(draft.Version, draft.Delivery, new ExchangeDto(7, [])));

        var ex = await Assert.ThrowsAsync<ValidationException>(async () =>
            await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), null)));

        Assert.Contains("pages.exchanges", ex.Errors.Keys);
        Assert.Empty(await host.QueryAsync(c => c.Snapshots.ToListAsync()));
    }

    [Fact]
    [Trait("Spec", "ST-3")]
    public async Task Publish_WhilePagesTheFooterLinksToAreStillDrafts_ListsEachDeadLink()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var ex = await Assert.ThrowsAsync<ValidationException>(async () =>
            await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), null)));

        Assert.Equal(8, ex.Errors.Count);
        Assert.All(ex.Errors.Values, v => Assert.Contains("not published", v.Single()));
    }

    [Fact]
    [Trait("Spec", "ST-14")]
    public async Task Publish_WithAStaleDraftVersion_IsRefused()
    {
        using var host = await PublishableAsync(postgres);
        var stale = await host.DraftVersionAsync();
        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        await host.SendAsync(new SaveAgeStepsCommand(draft.Version, draft.Ages));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new PublishSettingsCommand(stale, null)));
    }

    [Fact]
    [Trait("Spec", "ST-1a")]
    public async Task Publish_ADisabledEntry_TakesItsWholeSubtreeOutOfTheDocument()
    {
        using var host = await PublishableAsync(postgres);
        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        var tree = draft.Navigation.Select(n => n.Key == "girls" ? n with { Enabled = false } : n).ToList();
        await host.SendAsync(new SaveNavigationCommand(draft.Version, tree));

        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), null));

        var keys = Settings(await host.SendAsync(new GetPublishedSettingsQuery()))["navigation"]!.AsArray().Select(n => n!["key"]!.GetValue<string>()).ToArray();
        Assert.Equal(["boys", "new-in", "collections", "sale"], keys);
    }

    [Fact]
    [Trait("Spec", "ST-1")]
    public async Task Publish_Document_UsesTheStorefrontContractShape()
    {
        using var host = await PublishableAsync(postgres);
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), null));

        var settings = Settings(await host.SendAsync(new GetPublishedSettingsQuery()));

        Assert.Equal("923315613217", settings["brand"]!["whatsapp"]!.GetValue<string>());
        Assert.Equal("group", settings["navigation"]![0]!["kind"]!.GetValue<string>());
        Assert.True(settings["navigation"]![0]!["showAges"]!.GetValue<bool>());
        Assert.Equal("collection-list", settings["navigation"]![3]!["kind"]!.GetValue<string>());
        Assert.Equal(8, settings["ages"]!.AsArray().Count);
        Assert.Equal(7, settings["exchange"]!["windowDays"]!.GetValue<int>());
        Assert.Equal(("Islamabad", 300m), (settings["delivery"]![0]!["city"]!.GetValue<string>(), settings["delivery"]![0]!["charge"]!.GetValue<decimal>()));
        Assert.Equal(8, settings["pages"]!.AsArray().Count);
        Assert.Equal(["Dresses", "T-shirts", "Shorts"], settings["popularSearches"]!.AsArray().Select(s => s!.GetValue<string>()).ToArray());
    }

    [Fact]
    [Trait("Spec", "ST-12")]
    public async Task Restore_AnEarlierVersion_CreatesANewCurrentVersion_AndNeverEditsTheOldRow()
    {
        using var host = await PublishableAsync(postgres);
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "one"));
        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        await host.SendAsync(new SaveNavigationCommand(draft.Version, [Link("sale", "Final sale", "/sale", 1)]));
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "two"));

        var restored = await host.SendAsync(new RestoreSnapshotCommand(1, "oops"));

        Assert.Equal((3, true), (restored.VersionNo, restored.Created));
        var rows = await host.QueryAsync(c => c.Snapshots.OrderBy(s => s.VersionNo).ToListAsync());
        Assert.Equal([false, false, true], rows.Select(s => s.IsCurrent).ToArray());
        Assert.Contains("Restored version 1", rows[2].Note);
        host.Cache.Invalidate();
        var published = Settings(await host.SendAsync(new GetPublishedSettingsQuery()));
        Assert.Equal(3, published["version"]!.GetValue<int>());
        Assert.Equal("Girls", published["navigation"]![0]!["label"]!.GetValue<string>());

        var again = await host.SendAsync(new RestoreSnapshotCommand(3, null));
        Assert.False(again.Created);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new RestoreSnapshotCommand(99, null)));
    }

    [Fact]
    [Trait("Spec", "ST-12")]
    public async Task ListSnapshots_IsNewestFirst_PagedAndClamped()
    {
        using var host = await PublishableAsync(postgres);
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "one"));
        for (var i = 2; i <= 3; i++)
        {
            var draft = await host.SendAsync(new GetDraftSettingsQuery());
            await host.SendAsync(new SaveNavigationCommand(draft.Version, [Link("sale", $"Sale {i}", "/sale", 1)]));
            await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), $"v{i}"));
        }

        var first = await host.SendAsync(new ListSnapshotsQuery(1, 2));
        var second = await host.SendAsync(new ListSnapshotsQuery(2, 2));
        Assert.Equal([3, 2], first.Items.Select(s => s.VersionNo).ToArray());
        Assert.Equal([1], second.Items.Select(s => s.VersionNo).ToArray());
        Assert.True(first.Items[0].IsCurrent);
        Assert.Equal(3, first.TotalCount);

        var clamped = await host.SendAsync(new ListSnapshotsQuery(1, 9999));
        Assert.Equal(100, clamped.PageSize);
    }

    [Fact]
    [Trait("Spec", "ST-13")]
    public async Task PublishedSettings_AreCachedUntilThePublishedEventDropsTheCopy()
    {
        using var host = await PublishableAsync(postgres);
        Assert.Null(await host.SendAsync(new GetPublishedSettingsQuery()));

        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "one"));
        Assert.Equal(1, (await host.SendAsync(new GetPublishedSettingsQuery()))!.VersionNo);

        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        await host.SendAsync(new SaveNavigationCommand(draft.Version, [Link("sale", "Sale", "/sale", 1)]));
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "two"));
        Assert.Equal(1, (await host.SendAsync(new GetPublishedSettingsQuery()))!.VersionNo); // still the cached copy

        await new SettingsPublishedCacheInvalidator(host.Cache).HandleAsync(new Romp.Modules.Storefront.Contracts.StorefrontSettingsPublishedEvent(2, "x"), default);

        var fresh = await host.SendAsync(new GetPublishedSettingsQuery());
        Assert.Equal(2, fresh!.VersionNo);
        Assert.Equal($"\"{(await host.QueryAsync(c => c.Snapshots.SingleAsync(s => s.IsCurrent))).DocumentHash}\"", fresh.ETag);
    }

    [Fact]
    [Trait("Spec", "ST-8")]
    [Trait("Spec", "ST-9")]
    public async Task PublishedPage_FoundRedirectedGoneAndNotFound()
    {
        using var host = await PublishableAsync(postgres);
        var pages = (await host.SendAsync(new ListPagesQuery(PageSize: 100))).Items.ToDictionary(p => p.Slug);

        // Move "delivery" to "shipping" (the footer link follows), archive "terms", publish.
        var delivery = (await host.SendAsync(new GetPageQuery(pages["delivery"].Id)))!;
        await host.SendAsync(new UpdatePageCommand(delivery.Id, delivery.RowVersion, "shipping", delivery.Title, delivery.MetaDescription, delivery.Blocks));
        var terms = (await host.SendAsync(new GetPageQuery(pages["terms"].Id)))!;
        await host.SendAsync(new ChangePageStatusCommand(terms.Id, terms.RowVersion, PageStatus.Archived));
        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        var footer = draft.Footer.Select(c => c with
        {
            Links = [.. c.Links.Where(l => l.Target.Reference != "terms").Select(l => l.Target.Reference == "delivery" ? l with { Target = new TargetDto(LinkTargetKind.Page, "shipping") } : l)],
        }).ToList();
        await host.SendAsync(new SaveFooterCommand(draft.Version, footer));
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), null));

        var found = await host.SendAsync(new GetPublishedPageQuery("shipping"));
        Assert.Equal(PublishedPageOutcome.Found, found.Outcome);
        Assert.Equal("shipping", JsonNode.Parse(found.Json!)!["slug"]!.GetValue<string>());
        var moved = await host.SendAsync(new GetPublishedPageQuery("delivery"));
        Assert.Equal((PublishedPageOutcome.Redirect, "shipping"), (moved.Outcome, moved.RedirectSlug));
        Assert.Equal(PublishedPageOutcome.Gone, (await host.SendAsync(new GetPublishedPageQuery("terms"))).Outcome);
        Assert.Equal(PublishedPageOutcome.NotFound, (await host.SendAsync(new GetPublishedPageQuery("nothing-here"))).Outcome);
    }

    [Fact]
    [Trait("Spec", "ST-8")]
    public async Task Preview_ShowsDraftPagesAsLive_WithoutPublishing()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var json = JsonNode.Parse(await host.SendAsync(new PreviewSettingsQuery()))!;

        Assert.Equal(8, json["settings"]!["pages"]!.AsArray().Count);
        Assert.NotNull(json["pages"]!["delivery"]);
        Assert.Empty(await host.QueryAsync(c => c.Snapshots.ToListAsync()));
    }
}
