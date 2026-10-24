using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Domain.StorefrontSettings;
using static Romp.Modules.Storefront.Tests.StorefrontSettings.SettingsContentBuilder;

namespace Romp.Modules.Storefront.Tests.StorefrontSettings;

public sealed class PageRuleTests
{
    [Theory]
    [Trait("Spec", "ST-9")]
    [InlineData("size-guide")]
    [InlineData("our-story")]
    [InlineData("ab")]
    public void CheckSlug_ValidSlugs_AreAccepted(string slug) => Assert.Null(Page.CheckSlug(slug));

    [Theory]
    [Trait("Spec", "ST-4")]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Size-Guide")]
    [InlineData("size--guide")]
    [InlineData("-size")]
    [InlineData("size_guide")]
    [InlineData("size guide")]
    public void CheckSlug_BadPatterns_AreRejected(string slug) => Assert.NotNull(Page.CheckSlug(slug));

    [Theory]
    [Trait("Spec", "ST-4")]
    [InlineData("api")]
    [InlineData("girls")]
    [InlineData("new-in")]
    [InlineData("_foundations")]
    public void CheckSlug_ReservedRoutes_AreRejected(string slug) => Assert.NotNull(Page.CheckSlug(slug));

    [Fact]
    [Trait("Spec", "ST-9")]
    public void ChangeSlug_ReturnsTheOldSlugForTheRedirect()
    {
        var page = new Page("delivery", "Delivery", "How we deliver.");

        var old = page.ChangeSlug("shipping");

        Assert.Equal("delivery", old);
        Assert.Equal("shipping", page.Slug);
    }

    [Fact]
    [Trait("Spec", "ST-9")]
    public void ChangeSlug_ToAReservedSlug_Throws()
    {
        var page = new Page("delivery", "Delivery", "How we deliver.");

        Assert.Throws<DomainException>(() => page.ChangeSlug("api"));
        Assert.Equal("delivery", page.Slug);
    }

    [Fact]
    [Trait("Spec", "ST-17")]
    public void Validate_FirstHeadingNotLevelTwo_IsRejected()
    {
        var page = PublishedPage("delivery", new PageBlock(BlockType.Heading, "Big", level: 3));

        Assert.Contains(SettingsValidator.Validate(Valid(pages: [page])), p => p.Message.Contains("first heading must be a level 2"));
    }

    [Fact]
    [Trait("Spec", "ST-17")]
    public void Validate_FactBlockNamingAnUnknownFact_IsRejected()
    {
        var page = PublishedPage("delivery", new PageBlock(BlockType.Fact, factKey: "no.such.fact"));

        Assert.Contains(SettingsValidator.Validate(Valid(pages: [page])), p => p.Message.Contains("Choose one of the known facts"));
    }

    [Fact]
    [Trait("Spec", "ST-17")]
    public void Validate_PublishedPageQuotingAnEmptyFact_IsRejected()
    {
        var page = PublishedPage("exchanges", new PageBlock(BlockType.Fact, factKey: SettingsFacts.ExchangeConditions));

        var problems = SettingsValidator.Validate(Valid(pages: [page], exchange: new ExchangePolicy(7, [])));

        Assert.Contains(problems, p => p.Field == "pages.exchanges" && p.Message.Contains("still empty"));
    }

    [Fact]
    [Trait("Spec", "ST-17")]
    public void Validate_DraftPageQuotingAnEmptyFact_IsAllowed()
    {
        var page = new Page("exchanges", "Exchanges", "Our exchange policy.");
        page.ReplaceBlocks([new PageBlock(BlockType.Fact, factKey: SettingsFacts.ExchangeConditions)]);

        var problems = SettingsValidator.Validate(Valid(pages: [page], exchange: new ExchangePolicy(7, [])));

        Assert.Empty(problems);
    }

    [Fact]
    [Trait("Spec", "ST-17")]
    public void Validate_PublishedPageQuotingAFilledFact_IsAccepted()
    {
        var page = PublishedPage("delivery", new PageBlock(BlockType.Fact, factKey: SettingsFacts.DeliveryRules));

        Assert.Empty(SettingsValidator.Validate(Valid(pages: [page])));
    }

    [Fact]
    [Trait("Spec", "ST-17")]
    public void Validate_PublishedPageWithoutTitleOrDescription_IsRejected()
    {
        var page = new Page("delivery", "", "");
        page.SetStatus(PageStatus.Published);

        var problems = SettingsValidator.Validate(Valid(pages: [page]));

        Assert.Contains(problems, p => p.Message.Contains("page title"));
        Assert.Contains(problems, p => p.Message.Contains("search description"));
    }

    [Fact]
    [Trait("Spec", "ST-10")]
    public void Validate_MarkupInBlockText_IsKeptAsPlainText()
    {
        var page = PublishedPage("delivery", new PageBlock(BlockType.Paragraph, "<script>alert(1)</script>"));

        Assert.Empty(SettingsValidator.Validate(Valid(pages: [page])));
        Assert.Equal("<script>alert(1)</script>", page.Blocks[0].Text);
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_OverlongParagraphAndTooManyBlocks_AreRejected()
    {
        var long1 = new PageBlock(BlockType.Paragraph, new string('x', SettingsLimits.MaxParagraphLength + 1));
        var many = Enumerable.Range(0, SettingsLimits.MaxBlocksPerPage + 1).Select(_ => new PageBlock(BlockType.Paragraph, "x")).ToArray();

        Assert.Contains(SettingsValidator.Validate(Valid(pages: [PublishedPage("delivery", long1)])), p => p.Message.Contains("5000 characters"));
        Assert.Contains(SettingsValidator.Validate(Valid(pages: [PublishedPage("delivery", many)])), p => p.Message.Contains("at most 60 blocks"));
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_DuplicateSlugAcrossPages_IsRejected()
    {
        var problems = SettingsValidator.Validate(Valid(pages: [PublishedPage("delivery"), PublishedPage("delivery")]));

        Assert.Contains(problems, p => p.Message.Contains("used twice"));
    }
}
