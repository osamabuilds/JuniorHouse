using Romp.Modules.Storefront.Domain.Listings;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>Listings task 1 and 2: parsing, clamping and the same-variant rule (LST-1, 2, 11, 16).</summary>
public sealed class ListingFilterTests
{
    private static (ListingFilter? Filter, IReadOnlyList<ListingFilterError> Errors) Parse(
        string?[]? sizes = null, string?[]? colours = null, string? age = null, string? price = null, string? sort = null, string? page = null, string? pageSize = null) =>
        ListingFilter.Parse(sizes, colours, age, price, sort, page, pageSize);

    [Fact]
    [Trait("Spec", "LST-11")]
    public void NoInput_GivesTheDefaults()
    {
        var (filter, errors) = Parse();

        Assert.Empty(errors);
        Assert.Equal((ListingSort.Recent, 1, 24, false), (filter!.Sort, filter.Page, filter.PageSize, filter.HasAny));
        Assert.Equal(string.Empty, filter.ToCanonicalQuery());
    }

    [Theory]
    [Trait("Spec", "LST-11")]
    [InlineData("-5-10")]
    [InlineData("abc-10")]
    [InlineData("1e309-5")]
    [InlineData("10-5")]
    [InlineData("1000001-")]
    [InlineData("10")]
    [InlineData("1-2-3")]
    [InlineData("<script>-1")]
    public void AMalformedPrice_IsRefused(string price)
    {
        var (filter, errors) = Parse(price: price);

        Assert.Null(filter);
        Assert.Equal("price", Assert.Single(errors).Field);
    }

    [Theory]
    [Trait("Spec", "LST-11")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("99999999999999999999")]
    public void AMalformedPage_IsRefused(string page)
    {
        var (filter, errors) = Parse(page: page);

        Assert.Null(filter);
        Assert.Equal("page", Assert.Single(errors).Field);
    }

    [Fact]
    [Trait("Spec", "LST-11")]
    public void AHugePageThatIsStillANumber_ParsesAndAnswers404LaterNotAnError() =>
        Assert.Equal(999_999_999, Parse(page: "999999999").Filter!.Page);

    [Fact]
    [Trait("Spec", "LST-11")]
    public void ThePageSize_IsCappedAtFortyEight() =>
        Assert.Equal(48, Parse(pageSize: "5000").Filter!.PageSize);

    [Fact]
    [Trait("Spec", "LST-11")]
    public void ValuesThatAreNotCodes_AreIgnored_NeverEchoed()
    {
        var (filter, _) = Parse(sizes: ["5-6y", "<script>alert(1)</script>", "", null, "UPPER", "a b"], colours: ["navy", "../etc"]);

        Assert.Equal(["5-6y", "upper"], filter!.Sizes);
        Assert.Equal(["navy"], filter.Colours);
        Assert.DoesNotContain("script", filter.ToCanonicalQuery(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "LST-11")]
    public void ThousandsOfValues_AreCappedAtTwenty()
    {
        var many = Enumerable.Range(0, 5000).Select(i => $"s{i}").ToArray();

        Assert.Equal(ListingFilter.MaxValuesPerKey, Parse(sizes: many).Filter!.Sizes.Count);
    }

    [Fact]
    [Trait("Spec", "LST-11")]
    public void AnUnknownSort_FallsBackToRecent() =>
        Assert.Equal(ListingSort.Recent, Parse(sort: "'; drop table").Filter!.Sort);

    [Fact]
    [Trait("Spec", "LST-16")]
    public void AnAgeThatIsNotACode_IsIgnored()
    {
        Assert.Null(Parse(age: "<b>").Filter!.Age);
        Assert.Equal("age-2y", Parse(age: "AGE-2Y").Filter!.Age);
    }

    [Fact]
    [Trait("Spec", "LST-10")]
    public void TheCanonicalQuery_HasOneSpelling_WhateverTheInputOrder()
    {
        var a = Parse(sizes: ["7-8y", "5-6y"], colours: ["red"], age: "kids", price: "100-300", sort: "price-asc", page: "2").Filter!;
        var b = Parse(sizes: ["5-6y", "7-8y", "5-6y"], colours: ["red"], age: "KIDS", price: "100.00-300", sort: "price-asc", page: "2").Filter!;

        Assert.Equal("age=kids&size=5-6y&size=7-8y&colour=red&price=100-300&sort=price-asc&page=2", a.ToCanonicalQuery());
        Assert.Equal(a.ToCanonicalQuery(), b.ToCanonicalQuery());
        Assert.DoesNotContain("page", a.ToCanonicalQuery(includePage: false), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "LST-2")]
    public void OpenEndedPriceRanges_AreAccepted()
    {
        Assert.Equal((100m, (decimal?)null), (Parse(price: "100-").Filter!.PriceMin, Parse(price: "100-").Filter!.PriceMax));
        Assert.Equal(((decimal?)null, 300m), (Parse(price: "-300").Filter!.PriceMin, Parse(price: "-300").Filter!.PriceMax));
    }

    [Theory]
    [Trait("Spec", "LST-1")]
    [Trait("Spec", "LST-2")]
    [InlineData(true, "5-6y", "navy", 500, true)]
    [InlineData(false, "5-6y", "navy", 500, false)] // sold out never matches
    [InlineData(true, "3-4y", "navy", 500, false)] // wrong size
    [InlineData(true, "5-6y", "red", 500, false)] // wrong colour
    [InlineData(true, "5-6y", "navy", 50, false)] // below the range
    [InlineData(true, "5-6y", "navy", 5000, false)] // above the range
    public void AVariant_MatchesOnlyWhenEveryFilterHoldsOnIt(bool inStock, string size, string colour, int price, bool expected)
    {
        var filter = Parse(sizes: ["5-6y"], colours: ["navy"], price: "100-1000").Filter!;

        Assert.Equal(expected, ListingRules.Matches(inStock, size, colour, price, filter));
    }

    [Fact]
    [Trait("Spec", "LST-2")]
    public void SizeAndColourOnDifferentVariants_DoNotMatch_TheSameVariantRule()
    {
        var filter = Parse(sizes: ["5-6y"], colours: ["navy"]).Filter!;
        var variants = new[] { (Size: "5-6y", Colour: "red"), (Size: "3-4y", Colour: "navy") };

        Assert.DoesNotContain(variants, v => ListingRules.Matches(true, v.Size, v.Colour, 500, filter));
    }

    [Theory]
    [Trait("Spec", "LST-14")]
    [InlineData(1, 24, 0, false)]
    [InlineData(2, 24, 24, true)]
    [InlineData(2, 24, 25, false)]
    [InlineData(3, 24, 48, true)]
    [InlineData(int.MaxValue, 48, 100, true)]
    public void PastTheEnd_IsAPageAfterTheLast_ButPageOneAlwaysExists(int page, int size, int total, bool expected) =>
        Assert.Equal(expected, ListingRules.IsPastTheEnd(page, size, total));
}
