using Romp.Modules.Catalog.Domain.Urls;

namespace Romp.Modules.Catalog.Tests.Urls;

public sealed class ProductPathTests
{
    [Fact]
    [Trait("Spec", "URL-1")]
    [Trait("Spec", "SEO-02")]
    public void Build_UsesGenderCategoryAndSlug_InLowerCase()
    {
        var path = ProductPath.Build("GIRLS", "DRESSES", "floral-summer-dress");

        Assert.Equal("/girls/dresses/floral-summer-dress", path.ToString());
    }

    [Fact]
    [Trait("Spec", "URL-10")]
    public void ABuiltPath_NeverHasAnIdOrAQueryString()
    {
        var text = ProductPath.Build("UNISEX", "TOPS", "plain-tee").ToString();

        Assert.DoesNotContain('?', text);
        Assert.DoesNotContain('#', text);
        Assert.DoesNotMatch(@"/\d+(/|$)", text);
    }

    [Theory]
    [InlineData("GIRLS", "girls")]
    [InlineData("NEW_ARRIVALS", "new-arrivals")]
    [InlineData(" Boys ", "boys")]
    public void SegmentOf_LowerCasesAndHyphenates(string code, string expected) => Assert.Equal(expected, ProductPath.SegmentOf(code));

    [Theory]
    [InlineData("/girls/dresses/floral", "girls", "dresses", "floral")]
    [InlineData("girls/dresses/floral/", "girls", "dresses", "floral")]
    public void Parse_SplitsACanonicalPath(string path, string gender, string category, string slug) =>
        Assert.Equal(new ProductPath(gender, category, slug), ProductPath.Parse(path));

    [Theory]
    [InlineData("/girls/dresses/floral?size=4")]
    [InlineData("/girls/dresses/floral#top")]
    [InlineData("/girls/dresses")]
    [InlineData("/girls/dresses/floral/extra")]
    [InlineData("//girls/dresses")]
    [InlineData("")]
    [InlineData(null)]
    [Trait("Spec", "URL-10")]
    public void Parse_RefusesAnythingThatIsNotThreeSegments(string? path) => Assert.Null(ProductPath.Parse(path));

    [Theory]
    [InlineData("/Girls/Dresses/", "/girls/dresses")]
    [InlineData("/GIRLS/DRESSES/Floral-Summer-Dress/", "/girls/dresses/floral-summer-dress")]
    [InlineData("girls/dresses/floral", "/girls/dresses/floral")]
    [InlineData("/girls/dresses/floral", "/girls/dresses/floral")]
    [Trait("Spec", "URL-5")]
    public void Canonicalise_LowerCasesAndRemovesTheTrailingSlash(string path, string expected) =>
        Assert.Equal(expected, ProductPath.Canonicalise(path));
}
