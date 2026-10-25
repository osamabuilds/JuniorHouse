using Romp.Modules.Storefront.Domain;
using Romp.Modules.Storefront.Domain.Navigation;

namespace Romp.Modules.Storefront.Tests.Navigation;

/// <summary>Navigation task 1: the menu is built from lookup rows and shows only what has listable products (NAV-1, 3, 5, 8, 9).</summary>
public sealed class MenuBuilderTests
{
    private static readonly LookupRow[] Genders =
    [
        new("GIRLS", "Girls", 10, true), new("BOYS", "Boys", 20, true), new("UNISEX", "Unisex", 30, true),
    ];

    private static readonly LookupRow[] Categories =
    [
        new("DRESSES", "Dresses", 20, true), new("TEES_SHIRTS", "Tees and shirts", 10, true), new("OLD", "Old", 5, false),
    ];

    private static readonly LookupRow[] Collections = [new("SUMMER", "Summer", 10, true)];

    private static readonly LookupRow[] Ages = [new("AGE_1Y", "1Y", 10, true), new("AGE_2Y", "2Y", 20, true)];

    private static readonly HashSet<string> Hidden = ["UNISEX"];

    private static MenuAvailability Girls() => new()
    {
        Genders = new HashSet<string> { "GIRLS" },
        Categories = new HashSet<(string, string)> { ("GIRLS", "DRESSES"), ("GIRLS", "TEES_SHIRTS") },
        Collections = new HashSet<(string, string)> { ("GIRLS", "SUMMER") },
        Ages = new HashSet<(string, string)> { ("GIRLS", "AGE_2Y") },
        SaleGenders = new HashSet<string> { "GIRLS" },
    };

    private static MenuTree Build(MenuAvailability availability, IEnumerable<LookupRow>? categories = null) =>
        MenuBuilder.Build(Genders, categories ?? Categories, Collections, Ages, availability, Hidden);

    [Fact]
    [Trait("Spec", "NAV-1")]
    public void AnAudience_OffersNewIn_Sale_Categories_Collections_AndAges_AllScopedToIt()
    {
        var girls = Build(Girls()).Audiences.Single();

        Assert.Equal("/girls", girls.Path);
        Assert.Equal(
            [("NewIn", "/girls/new-in"), ("Sale", "/girls/sale"), ("Category", "/girls/tees-shirts"), ("Category", "/girls/dresses"), ("Collection", "/collections/summer"), ("Age", "/girls?age=age-2y")],
            girls.Entries.Select(e => (e.Kind.ToString(), e.Path)).ToArray());
    }

    [Fact]
    [Trait("Spec", "NAV-3")]
    public void ACategoryOrAgeWithNoListableProducts_IsNotOffered()
    {
        var availability = new MenuAvailability
        {
            Genders = new HashSet<string> { "GIRLS" },
            Categories = new HashSet<(string, string)> { ("GIRLS", "DRESSES") },
        };

        var entries = Build(availability).Audiences.Single().Entries;

        Assert.DoesNotContain(entries, e => e.Code == "TEES_SHIRTS" || e.Kind is MenuEntryKind.Age or MenuEntryKind.Collection or MenuEntryKind.Sale);
        Assert.Contains(entries, e => e.Code == "DRESSES");
    }

    [Fact]
    [Trait("Spec", "NAV-3")]
    public void AnAudienceWithNoListableProducts_IsNotOffered()
    {
        var menu = Build(new MenuAvailability());

        Assert.Empty(menu.Audiences);
        Assert.False(menu.HasNewIn);
        Assert.False(menu.HasSale);
    }

    [Fact]
    [Trait("Spec", "NAV-3")]
    public void ARetiredLookupRow_IsNeverOffered_EvenWithProducts()
    {
        var availability = new MenuAvailability
        {
            Genders = new HashSet<string> { "GIRLS" },
            Categories = new HashSet<(string, string)> { ("GIRLS", "OLD") },
        };

        Assert.DoesNotContain(Build(availability).Audiences.Single().Entries, e => e.Code == "OLD");
    }

    [Fact]
    [Trait("Spec", "NAV-5")]
    public void ANewLookupRow_AppearsWithNoCodeChange()
    {
        var withNew = Categories.Append(new LookupRow("SWIMWEAR", "Swimwear", 1, true));
        var availability = new MenuAvailability
        {
            Genders = new HashSet<string> { "GIRLS" },
            Categories = new HashSet<(string, string)> { ("GIRLS", "SWIMWEAR") },
        };

        var entry = Build(availability, withNew).Audiences.Single().Entries.Single(e => e.Kind == MenuEntryKind.Category);

        Assert.Equal(("Swimwear", "/girls/swimwear"), (entry.Label, entry.Path));
    }

    [Fact]
    [Trait("Spec", "NAV-9")]
    public void ARenamedAge_KeepsItsUrl_BecauseTheParameterIsTheCode()
    {
        var renamed = new[] { new LookupRow("AGE_2Y", "Two years", 20, true) };
        var tree = MenuBuilder.Build(Genders, Categories, Collections, renamed, Girls(), Hidden);

        var age = tree.Audiences.Single().Entries.Single(e => e.Kind == MenuEntryKind.Age);

        Assert.Equal("Two years", age.Label);
        Assert.Equal("/girls?age=age-2y", age.Path);
    }

    [Fact]
    [Trait("Spec", "NAV-1")]
    public void AHiddenGender_IsNotAHeaderEntry()
    {
        var availability = new MenuAvailability { Genders = new HashSet<string> { "GIRLS", "UNISEX" } };

        Assert.Equal(["GIRLS"], Build(availability).Audiences.Select(a => a.Code).ToArray());
    }

    [Fact]
    [Trait("Spec", "NAV-1")]
    public void Audiences_FollowTheirSortOrder_NotTheirCodes()
    {
        var availability = new MenuAvailability { Genders = new HashSet<string> { "BOYS", "GIRLS" } };

        Assert.Equal(["GIRLS", "BOYS"], Build(availability).Audiences.Select(a => a.Code).ToArray());
    }

    [Fact]
    [Trait("Spec", "NAV-1")]
    public void TheSharedCollections_ListEachCollectionOnceAcrossAudiences()
    {
        var availability = new MenuAvailability
        {
            Genders = new HashSet<string> { "GIRLS", "BOYS" },
            Collections = new HashSet<(string, string)> { ("GIRLS", "SUMMER"), ("BOYS", "SUMMER") },
            SaleGenders = new HashSet<string> { "BOYS" },
        };

        var menu = Build(availability);

        Assert.Equal(["/collections/summer"], menu.Collections.Select(c => c.Path).ToArray());
        Assert.True(menu.HasSale);
        Assert.DoesNotContain(menu.Audiences.First(a => a.Code == "GIRLS").Entries, e => e.Kind == MenuEntryKind.Sale);
    }
}

/// <summary>Navigation task 1: the trail and the structured data come from one list (NAV-4, SEO-03).</summary>
public sealed class BreadcrumbTrailTests
{
    private static readonly MenuAudienceRef Girls = new("GIRLS", "Girls");
    private static readonly MenuCategoryRef Dresses = new("DRESSES", "Dresses");

    [Fact]
    [Trait("Spec", "NAV-4")]
    public void AProductPage_HasHomeAudienceCategoryAndTheProduct_TheLastNotALink()
    {
        var trail = BreadcrumbTrail.For(Girls, Dresses, "Floral dress");

        Assert.Equal(
            [("Home", "/"), ("Girls", "/girls"), ("Dresses", "/girls/dresses"), ("Floral dress", null)],
            trail.Select(c => (c.Label, c.Path)).ToArray());
    }

    [Fact]
    [Trait("Spec", "NAV-4")]
    public void ACategoryListing_EndsOnTheCategory_WithNoLink()
    {
        var trail = BreadcrumbTrail.For(Girls, Dresses, null);

        Assert.Equal(["Home", "Girls", "Dresses"], trail.Select(c => c.Label).ToArray());
        Assert.Null(trail[^1].Path);
        Assert.All(trail.Take(2), c => Assert.NotNull(c.Path));
    }

    [Fact]
    [Trait("Spec", "NAV-4")]
    public void TheHomePage_HasNoTrail() => Assert.Empty(BreadcrumbTrail.For(null, null, null));

    [Fact]
    [Trait("Spec", "NAV-4")]
    public void AnUntiedListing_IsHomeThenThePage() =>
        Assert.Equal([("Home", "/"), ("Sale", null)], BreadcrumbTrail.ForPage("Sale").Select(c => (c.Label, c.Path)).ToArray());

    [Fact]
    [Trait("Spec", "NAV-4")]
    public void ACategoryWithoutAnAudience_IsIgnored_SoNoBrokenLinkIsBuilt() =>
        Assert.Equal(["Home", "Floral dress"], BreadcrumbTrail.For(null, Dresses, "Floral dress").Select(c => c.Label).ToArray());
}
