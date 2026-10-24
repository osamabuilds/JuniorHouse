using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Storefront.Application.StorefrontSettings;
using Romp.Modules.Storefront.Domain.StorefrontSettings;
using Romp.Modules.Storefront.Tests.Support;
using static Romp.Modules.Storefront.Tests.Support.TestData;

namespace Romp.Modules.Storefront.Tests.StorefrontSettings;

/// <summary>SCRUM-206 task 8: page commands and queries.</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class PageCommandTests(StorefrontPostgres postgres)
{
    [Fact]
    [Trait("Spec", "ST-10")]
    public async Task CreatePage_MarkupInBlocks_IsStoredAsPlainText()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var page = await host.SendAsync(new CreatePageCommand("care-guide", "Care guide", "How to care for your clothes.",
            [Heading("<b>Washing</b>"), Paragraph("<script>alert(1)</script>"), Fact(SettingsFacts.ContactEmail, "Email")]));

        var again = (await host.SendAsync(new GetPageQuery(page.Id)))!;
        Assert.Equal(PageStatus.Draft, again.Status);
        Assert.Equal("<b>Washing</b>", again.Blocks[0].Text);
        Assert.Equal("<script>alert(1)</script>", again.Blocks[1].Text);
        Assert.Equal(["Heading", "Paragraph", "Fact"], again.Blocks.Select(b => b.Type.ToString()).ToArray());
        Assert.Equal(SettingsFacts.ContactEmail, again.Blocks[2].FactKey);
    }

    [Theory]
    [Trait("Spec", "ST-4")]
    [Trait("Spec", "ST-9")]
    [InlineData("api")]
    [InlineData("girls")]
    [InlineData("Bad Slug")]
    [InlineData("delivery")]
    public async Task CreatePage_ReservedBadOrTakenSlug_IsRejected(string slug)
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new CreatePageCommand(slug, "T", "D", [])));

        Assert.NotEmpty(ex.Errors);
        Assert.Equal(8, (await host.SendAsync(new ListPagesQuery())).TotalCount);
    }

    [Fact]
    [Trait("Spec", "ST-9")]
    public async Task UpdatePage_SlugChangeOfAPublishedPage_WritesA301RowForTheOldSlug()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var row = (await host.SendAsync(new ListPagesQuery())).Items.Single(p => p.Slug == "delivery");
        var page = (await host.SendAsync(new GetPageQuery(row.Id)))!;
        var published = await host.SendAsync(new ChangePageStatusCommand(page.Id, page.RowVersion, PageStatus.Published));

        var moved = await host.SendAsync(new UpdatePageCommand(page.Id, published.RowVersion, "shipping", page.Title, page.MetaDescription, page.Blocks));

        Assert.Equal("shipping", moved.Slug);
        var redirect = Assert.Single(await host.QueryAsync(c => c.PageRedirects.ToListAsync()));
        Assert.Equal(("delivery", page.Id), (redirect.OldSlug, redirect.PageId));
    }

    [Fact]
    [Trait("Spec", "ST-9")]
    public async Task UpdatePage_SlugChangeOfADraft_WritesNoRedirect_AndTheOldSlugCanBeReused()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var page = await host.SendAsync(new CreatePageCommand("gift-ideas", "Gifts", "Ideas.", []));

        var renamed = await host.SendAsync(new UpdatePageCommand(page.Id, page.RowVersion, "gifts", "Gifts", "Ideas.", []));

        Assert.Empty(await host.QueryAsync(c => c.PageRedirects.ToListAsync()));
        await host.SendAsync(new CreatePageCommand("gift-ideas", "Gifts again", "More.", []));
        Assert.Equal("gifts", renamed.Slug);
    }

    [Fact]
    [Trait("Spec", "ST-9")]
    public async Task CreatePage_OnAnOldAddressOfAnotherPage_IsRejected()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var row = (await host.SendAsync(new ListPagesQuery())).Items.Single(p => p.Slug == "contact");
        var page = (await host.SendAsync(new GetPageQuery(row.Id)))!;
        var live = await host.SendAsync(new ChangePageStatusCommand(page.Id, page.RowVersion, PageStatus.Published));
        await host.SendAsync(new UpdatePageCommand(page.Id, live.RowVersion, "contact-us", page.Title, page.MetaDescription, page.Blocks));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new CreatePageCommand("contact", "New", "D", [])));

        Assert.Contains("old address", ex.Errors["pages.contact"].Single());
    }

    [Fact]
    [Trait("Spec", "ST-14")]
    public async Task UpdatePage_WithAStaleRowVersion_IsRefused_EvenWhenOnlyBlocksChange()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var row = (await host.SendAsync(new ListPagesQuery())).Items.Single(p => p.Slug == "our-story");
        var page = (await host.SendAsync(new GetPageQuery(row.Id)))!;
        await host.SendAsync(new UpdatePageCommand(page.Id, page.RowVersion, page.Slug, page.Title, page.MetaDescription, [Paragraph("first")]));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            host.SendAsync(new UpdatePageCommand(page.Id, page.RowVersion, page.Slug, page.Title, page.MetaDescription, [Paragraph("second")])));

        var after = (await host.SendAsync(new GetPageQuery(page.Id)))!;
        Assert.Equal("first", Assert.Single(after.Blocks).Text);
    }

    [Fact]
    [Trait("Spec", "ST-17")]
    public async Task ChangePageStatus_PublishingAPageWithAnEmptyFact_IsRefused()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var draft = await host.SendAsync(new GetDraftSettingsQuery());
        await host.SendAsync(new SaveDeliveryAndExchangeCommand(draft.Version, draft.Delivery, new ExchangeDto(7, [])));
        var row = (await host.SendAsync(new ListPagesQuery())).Items.Single(p => p.Slug == "exchanges");
        var page = (await host.SendAsync(new GetPageQuery(row.Id)))!;

        var ex = await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ChangePageStatusCommand(page.Id, page.RowVersion, PageStatus.Published)));

        Assert.Contains("exchange.conditions", ex.Errors["pages.exchanges"].Single());
        Assert.Equal(PageStatus.Draft, (await host.SendAsync(new GetPageQuery(page.Id)))!.Status);
    }

    [Fact]
    [Trait("Spec", "ST-8")]
    public async Task ListPages_IsPagedBySlug_FilteredByStatus_AndClamped()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var first = await host.SendAsync(new ListPagesQuery(Page: 1, PageSize: 3));
        var second = await host.SendAsync(new ListPagesQuery(Page: 2, PageSize: 3));
        Assert.Equal(8, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.Equal(["contact", "delivery", "exchanges"], first.Items.Select(p => p.Slug).ToArray());
        Assert.Equal(["made-in-pakistan", "our-story", "privacy"], second.Items.Select(p => p.Slug).ToArray());

        var clamped = await host.SendAsync(new ListPagesQuery(Page: -4, PageSize: 5000));
        Assert.Equal((1, 100), (clamped.Page, clamped.PageSize));

        Assert.Equal(0, (await host.SendAsync(new ListPagesQuery(PageStatus.Published))).TotalCount);
        Assert.Equal(5, (await host.SendAsync(new GetDraftSettingsQuery())).Pages.Single(p => p.Slug == "delivery").BlockCount);
    }

    [Fact]
    [Trait("Spec", "ST-8")]
    public async Task ListPages_RunsAConstantNumberOfQueries_WhateverThePageSize()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var counter = new QueryCounter();

        await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var small = await new ListPagesQueryHandler(context).Handle(new ListPagesQuery(PageSize: 2), default);
        var smallCount = counter.Count;
        counter.Reset();
        var large = await new ListPagesQueryHandler(context).Handle(new ListPagesQuery(PageSize: 100), default);

        Assert.Equal(2, smallCount);
        Assert.Equal(smallCount, counter.Count);
        Assert.Equal(2, small.Items.Count);
        Assert.Equal(8, large.Items.Count);
    }
}
