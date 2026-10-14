using Romp.Modules.Catalog.Domain.ProductMedia;

namespace Romp.Modules.Catalog.Tests.ProductMedia;

public sealed class AltTextTests
{
    [Theory]
    [InlineData("A red floral summer dress, front view")]
    [InlineData("Boys' navy hoodie with a white zip")]
    [InlineData("Close-up of the button placket")]
    [Trait("Spec", "MED-4")]
    public void RealDescriptions_AreAccepted(string text) => Assert.Null(AltText.Validate(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Spec", "MED-2")]
    public void EmptyOrWhitespace_IsRefused(string? text) => Assert.NotNull(AltText.Validate(text));

    [Theory]
    [InlineData("IMG_1234.jpg")]
    [InlineData("DSC00012.JPG")]
    [InlineData("photo.png")]
    [InlineData("front-view.webp")]
    [InlineData("IMG 4521")]
    [InlineData("DSCN0042")]
    [InlineData("image12")]
    [Trait("Spec", "MED-2")]
    public void AFileName_IsNotADescription(string text) => Assert.Contains("file name", AltText.Validate(text), StringComparison.Ordinal);

    [Theory]
    [InlineData("dress dress dress dress dress")]
    [InlineData("kids dress, girls dress, baby dress, cheap dress, best dress")]
    [Trait("Spec", "MED-2")]
    public void RepeatedKeywords_AreRefused(string text) => Assert.Contains("repeats", AltText.Validate(text), StringComparison.Ordinal);

    [Theory]
    [InlineData("12345")]
    [InlineData("... --- ...")]
    [Trait("Spec", "MED-2")]
    public void OnlyDigitsOrPunctuation_IsRefused(string text) => Assert.NotNull(AltText.Validate(text));

    [Fact]
    [Trait("Spec", "MED-3")]
    public void MoreThan125Characters_IsRefused()
    {
        var long125 = string.Join(' ', Enumerable.Range(0, 25).Select(i => "word" + (char)('a' + i)));
        var ok = long125[..125].Trim();
        Assert.Null(AltText.Validate(ok));
        Assert.Contains("125", AltText.Validate(ok + " plus some more words to go over the limit of one hundred twenty five"), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "MED-4")]
    public void MarkupCharacters_AreRefused_SoItIsPlainText() =>
        Assert.NotNull(AltText.Validate("<script>alert(1)</script> dress"));

    [Fact]
    public void Suggest_UsesTheProductNameColourAndView()
    {
        Assert.Equal("Floral Dress in Red, view 2", AltText.Suggest("Floral Dress", "Red", 2));
        Assert.Equal("Floral Dress, view 1", AltText.Suggest("Floral Dress", null, 1));
        Assert.Null(AltText.Validate(AltText.Suggest("Floral Dress", "Red", 2)));
    }

    [Fact]
    public void Suggest_NeverExceedsTheLimit() =>
        Assert.True(AltText.Suggest(new string('x', 300), "Red", 1).Length <= AltText.MaxLength);
}
