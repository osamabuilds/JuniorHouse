using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.StorefrontSettings;
using Romp.Modules.Storefront.Domain.StorefrontSettings;
using Romp.Modules.Storefront.Tests.Support;
using static Romp.Modules.Storefront.Tests.Support.TestData;

namespace Romp.Modules.Storefront.Tests.StorefrontSettings;

/// <summary>SCRUM-206 tasks 14 and 15: who and when on every change, and the page list the sitemap reads.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SitemapAndAuditTests(StorefrontPostgres postgres)
{
    [Fact]
    [Trait("Spec", "ST-8")]
    public async Task Sitemap_ListsPublishedPages_AndDropsAnArchivedOneOnceRepublished()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        Assert.Empty(await host.ListSitemapPagesAsync()); // nothing published yet

        await host.PublishAllPagesAsync();
        var guide = await host.SendAsync(new CreatePageCommand("gift-guide", "Gift guide", "Gifts for little ones.", [Paragraph("Ideas.")]));
        var live = await host.SendAsync(new ChangePageStatusCommand(guide.Id, guide.RowVersion, PageStatus.Published));
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "with gift guide"));
        host.Cache.Invalidate();

        var pages = await host.ListSitemapPagesAsync();
        Assert.Equal(9, pages.Count);
        Assert.Contains(pages, p => p is { Slug: "gift-guide", Title: "Gift guide" });
        var snapshot = await host.QueryAsync(c => c.Snapshots.SingleAsync(s => s.IsCurrent));
        Assert.All(pages, p => Assert.Equal(snapshot.InsrDte, p.LastModified));

        await host.SendAsync(new ChangePageStatusCommand(guide.Id, live.RowVersion, PageStatus.Archived));
        host.Cache.Invalidate();
        Assert.Equal(9, (await host.ListSitemapPagesAsync()).Count); // the archive is not visible until it is published

        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "archive gift guide"));
        host.Cache.Invalidate();
        Assert.DoesNotContain(await host.ListSitemapPagesAsync(), p => p.Slug == "gift-guide");
    }

    [Fact]
    [Trait("Spec", "ST-12")]
    public async Task EveryChange_IsStampedWithWhoAndWhen_AndPublishKeepsItsNote()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        await host.SendAsync(new SaveNavigationCommand(draft.Version, [Link("sale", "Sale", "/sale", 1)]));
        var page = await host.SendAsync(new CreatePageCommand("care-tips", "Care tips", "Looking after clothes.", []));
        await host.SendAsync(new UpdatePageCommand(page.Id, page.RowVersion, "care-tips", "Care tips", "Looking after clothes well.", []));
        await host.PublishAllPagesAsync();
        await host.SendAsync(new PublishSettingsCommand(await host.DraftVersionAsync(), "March launch"));

        var item = await host.QueryAsync(c => c.NavItems.SingleAsync());
        Assert.True(item.InsrDte > before);
        Assert.False(string.IsNullOrWhiteSpace(item.InsrBy));

        var edited = await host.QueryAsync(c => c.Pages.SingleAsync(p => p.Slug == "care-tips"));
        Assert.True(edited.UpdtDte > before);
        Assert.False(string.IsNullOrWhiteSpace(edited.UpdtBy));

        var snapshot = await host.QueryAsync(c => c.Snapshots.SingleAsync());
        Assert.Equal("March launch", snapshot.Note);
        Assert.True(snapshot.InsrDte > before);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.InsrBy));
    }
}
