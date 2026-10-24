using Romp.Modules.Storefront.Domain.StorefrontSettings;
using static Romp.Modules.Storefront.Tests.StorefrontSettings.SettingsContentBuilder;

namespace Romp.Modules.Storefront.Tests.StorefrontSettings;

public sealed class NavigationRuleTests
{
    [Fact]
    [Trait("Spec", "ST-1")]
    public void Validate_ValidDraft_HasNoProblems() =>
        Assert.Empty(SettingsValidator.Validate(Valid()));

    [Fact]
    [Trait("Spec", "ST-1a")]
    public void Validate_ThreeLevels_IsAccepted()
    {
        var third = Link("frocks", "Frocks", "/girls/dresses/frocks", 1);
        var second = Link("dresses", "Dresses", "/girls/dresses", 1).AddChild(third);
        var root = Link("girls", "Girls", "/girls", 1).AddChild(second);

        Assert.Empty(SettingsValidator.Validate(Valid([root])));
    }

    [Fact]
    [Trait("Spec", "ST-1a")]
    [Trait("Spec", "ST-4")]
    public void Validate_FourthLevel_IsRejectedWithTheLevelsMessage()
    {
        var fourth = Link("party", "Party", "/girls/dresses/frocks/party", 1);
        var third = Link("frocks", "Frocks", "/girls/dresses/frocks", 1).AddChild(fourth);
        var second = Link("dresses", "Dresses", "/girls/dresses", 1).AddChild(third);
        var root = Link("girls", "Girls", "/girls", 1).AddChild(second);

        var problem = Assert.Single(SettingsValidator.Validate(Valid([root])));

        Assert.Equal("navigation.girls.dresses.frocks.party", problem.Field);
        Assert.Contains("Only 3 levels are allowed", problem.Message);
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_DuplicateKey_IsRejectedOnTheSecondEntry()
    {
        var problems = SettingsValidator.Validate(Valid([Link("girls", "Girls", "/girls", 1), Link("girls", "Also girls", "/boys", 2)]));

        var problem = Assert.Single(problems);
        Assert.Contains("used twice", problem.Message);
    }

    [Theory]
    [Trait("Spec", "ST-4")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A label that is far longer than the forty characters allowed")]
    public void Validate_BadLabel_IsRejected(string label)
    {
        var problems = SettingsValidator.Validate(Valid([Link("girls", label, "/girls", 1)]));

        Assert.Contains(problems, p => p.Field == "navigation.girls" && p.Message.Contains("label of 1 to 40"));
    }

    [Fact]
    [Trait("Spec", "ST-1")]
    public void Validate_SiblingsSharingASortPosition_AreRejected()
    {
        var problems = SettingsValidator.Validate(Valid([Link("girls", "Girls", "/girls", 1), Link("boys", "Boys", "/boys", 1)]));

        Assert.Contains(problems, p => p.Message.Contains("share position 1"));
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_GroupWithoutAnEnabledMember_IsRejected()
    {
        var group = Group("shop", "Shop", 2).AddChild(Link("sale", "Sale", "/sale", 1, enabled: false));

        var problems = SettingsValidator.Validate(Valid([Link("girls", "Girls", "/girls", 1), group]));

        Assert.Contains(problems, p => p.Field == "navigation.shop" && p.Message.Contains("nothing in it"));
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_MenuWithNoLinks_IsRejected()
    {
        var problems = SettingsValidator.Validate(Valid([Link("girls", "Girls", "/girls", 1, enabled: false)]));

        Assert.Contains(problems, p => p.Field == "navigation" && p.Message.Contains("no links"));
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_LinkWithoutATarget_IsRejected()
    {
        var item = new NavItem("girls", "Girls", NavKind.Link, null, 1);

        var problems = SettingsValidator.Validate(Valid([item]));

        Assert.Contains(problems, p => p.Message.Contains("Choose where"));
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_LinkToAPageThatIsNotPublished_IsRejected()
    {
        var item = new NavItem("help", "Help", NavKind.Link, new LinkTarget(LinkTargetKind.Page, "no-such-page"), 1);

        var problems = SettingsValidator.Validate(Valid([item]));

        Assert.Contains(problems, p => p.Message.Contains("does not exist or is not published"));
    }

    [Theory]
    [Trait("Spec", "ST-4")]
    [InlineData(LinkTargetKind.Path, "javascript:alert(1)")]
    [InlineData(LinkTargetKind.Path, "//evil.example/x")]
    [InlineData(LinkTargetKind.Path, "/Girls")]
    [InlineData(LinkTargetKind.External, "http://instagram.com/romp")]
    [InlineData(LinkTargetKind.External, "https://evil.example/phish")]
    [InlineData(LinkTargetKind.External, "https://user:pw@instagram.com/")]
    [InlineData(LinkTargetKind.Collection, "Bad Code")]
    public void Check_UnsafeOrMalformedTargets_AreRejected(LinkTargetKind kind, string reference) =>
        Assert.NotNull(LinkTarget.Check(kind, reference, ["instagram.com"]));

    [Theory]
    [Trait("Spec", "ST-4")]
    [InlineData(LinkTargetKind.Path, "/")]
    [InlineData(LinkTargetKind.Path, "/girls/dresses")]
    [InlineData(LinkTargetKind.External, "https://instagram.com/romp")]
    [InlineData(LinkTargetKind.Collection, "summer-edit")]
    [InlineData(LinkTargetKind.Page, "size-guide")]
    public void Check_ValidTargets_AreAccepted(LinkTargetKind kind, string reference) =>
        Assert.Null(LinkTarget.Check(kind, reference, ["instagram.com"]));

    [Fact]
    [Trait("Spec", "ST-4")]
    public void Validate_MoreThanTheNavigationCap_IsRejected()
    {
        var items = Enumerable.Range(1, 101).Select(i => Link($"item-{i}", $"Item {i}", "/girls", i)).ToArray();

        Assert.Contains(SettingsValidator.Validate(Valid(items)), p => p.Field == "navigation" && p.Message.Contains("At most 100"));
    }

    [Fact]
    [Trait("Spec", "ST-2")]
    public void Validate_DuplicateAgeCode_IsRejected()
    {
        var problems = SettingsValidator.Validate(Valid(ages: [new AgeStep("1y", "1 year", 1), new AgeStep("1y", "One", 2)]));

        Assert.Contains(problems, p => p.Field == "ages.1y" && p.Message.Contains("used twice"));
    }
}
