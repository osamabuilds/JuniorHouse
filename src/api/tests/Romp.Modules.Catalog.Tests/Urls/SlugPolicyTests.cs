using Romp.Modules.Catalog.Domain.Urls;

namespace Romp.Modules.Catalog.Tests.Urls;

public sealed class SlugPolicyTests
{
    private static readonly string[] Reserved = ["cart", "checkout", "account", "admin", "search"];

    [Theory]
    [InlineData("Floral Summer Dress", "floral-summer-dress")]
    [InlineData("  Boys' Denim   Jacket!! ", "boys-denim-jacket")]
    [InlineData("Café Crème Romper", "cafe-creme-romper")]
    [InlineData("The Dress for the Girl", "dress-girl")]
    [InlineData("Tee (3-pack) - Blue/Red", "tee-3-pack-blue-red")]
    [InlineData("../../etc/passwd", "etc-passwd")]
    [InlineData("%2e%2e/admin", "2e-2e-admin")]
    [InlineData("Ｆｌｏｒａｌ", "floral")]
    [InlineData("ПЛАТЬЕ", "")]
    [InlineData("___", "")]
    [Trait("Spec", "URL-6")]
    public void Normalise_TurnsAnyTextIntoALowerCaseHyphenatedAsciiSlug(string text, string expected)
    {
        var slug = SlugPolicy.Normalise(text);

        Assert.Equal(expected, slug);
        Assert.True(slug.Length == 0 || SlugPolicy.Validate(slug, [], 80) is null);
    }

    [Fact]
    [Trait("Spec", "URL-8")]
    public void Normalise_CutsALongNameOnAWordBoundary_WithinTheMaximum()
    {
        var name = string.Join(' ', Enumerable.Repeat("extraordinarily", 20));

        var slug = SlugPolicy.Normalise(name);

        Assert.True(slug.Length <= 80);
        Assert.False(slug.EndsWith('-'));
        Assert.StartsWith("extraordinarily-extraordinarily", slug, StringComparison.Ordinal);
        Assert.DoesNotContain("extra-", slug, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "URL-8")]
    public void Normalise_CutsAWordLongerThanTheMaximumHard()
    {
        Assert.Equal(80, SlugPolicy.Normalise(new string('a', 200)).Length);
    }

    [Theory]
    [InlineData("floral-dress", null)]
    [InlineData("a", null)]
    [InlineData("dress-2", null)]
    [Trait("Spec", "URL-6")]
    public void Validate_AcceptsAClosedAlphabet(string slug, string? expected) =>
        Assert.Equal(expected, SlugPolicy.Validate(slug, Reserved, 80));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Floral")]
    [InlineData("floral dress")]
    [InlineData("floral--dress")]
    [InlineData("-floral")]
    [InlineData("floral-")]
    [InlineData("floral_dress")]
    [InlineData("café")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("%2e")]
    [InlineData("floralа")]
    [Trait("Spec", "URL-6")]
    public void Validate_RefusesAnythingOutsideTheAlphabet(string? slug) =>
        Assert.NotNull(SlugPolicy.Validate(slug, Reserved, 80));

    [Theory]
    [InlineData("cart")]
    [InlineData("admin")]
    [InlineData("search")]
    [Trait("Spec", "URL-7")]
    public void Validate_RefusesAReservedWord(string slug) => Assert.Contains("reserved", SlugPolicy.Validate(slug, Reserved, 80), StringComparison.Ordinal);

    [Fact]
    [Trait("Spec", "URL-8")]
    public void Validate_RefusesASlugOverTheMaximum()
    {
        Assert.Null(SlugPolicy.Validate(new string('a', 80), Reserved, 80));
        Assert.NotNull(SlugPolicy.Validate(new string('a', 81), Reserved, 80));
    }

    [Theory]
    [InlineData("floral-dress", 2, "floral-dress-2")]
    [InlineData("floral-dress", 10, "floral-dress-10")]
    [Trait("Spec", "URL-4")]
    public void WithSuffix_AddsAStableNumber(string slug, int number, string expected) =>
        Assert.Equal(expected, SlugPolicy.WithSuffix(slug, number));

    [Fact]
    [Trait("Spec", "URL-4")]
    public void WithSuffix_StaysWithinTheMaximum_AndNeverLeavesADoubleHyphen()
    {
        var slug = SlugPolicy.WithSuffix(SlugPolicy.Normalise(string.Join(' ', Enumerable.Repeat("long", 40))), 12);

        Assert.True(slug.Length <= 80);
        Assert.EndsWith("-12", slug, StringComparison.Ordinal);
        Assert.Null(SlugPolicy.Validate(slug, [], 80));
    }
}
