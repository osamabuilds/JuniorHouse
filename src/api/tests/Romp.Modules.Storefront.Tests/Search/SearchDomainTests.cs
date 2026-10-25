using System.Text;
using Romp.Modules.Storefront.Domain.Search;

namespace Romp.Modules.Storefront.Tests.Search;

/// <summary>Search task 3: normalising, tokenising, SKU shape, miss redaction and synonym expansion (SRC-3, 5, 12, 13).</summary>
public sealed class SearchDomainTests
{
    [Theory]
    [Trait("Spec", "SRC-3")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  Floral   DRESS ", "floral dress")]
    [InlineData("dung\u0000aree", "dungaree")]
    [InlineData("dun‮garee", "dungaree")]
    [InlineData("a​b", "ab")]
    [InlineData("ＦＬＯＲＡＬ", "floral")]
    [InlineData("a\tb\nc", "a b c")]
    public void Normalise_RemovesControlsAndFoldsCase(string? raw, string expected) =>
        Assert.Equal(expected, SearchQuery.Normalise(raw));

    [Fact]
    [Trait("Spec", "SRC-3")]
    public void Normalise_CutsVeryLongInputAtTheCap()
    {
        var text = SearchQuery.Normalise(new string('a', 10_000));

        Assert.Equal(SearchQuery.MaxLength, text.Length);
    }

    [Fact]
    [Trait("Spec", "SRC-3")]
    public void Normalise_NeverSplitsASurrogatePairAtTheCut()
    {
        var text = SearchQuery.Normalise(new string('a', SearchQuery.MaxLength - 1) + "😀😀");

        Assert.True(text.Length <= SearchQuery.MaxLength);
        Assert.DoesNotContain(text.EnumerateRunes(), r => r == Rune.ReplacementChar);
    }

    [Fact]
    [Trait("Spec", "SRC-3")]
    public void Normalise_LoneSurrogate_IsHandledWithoutThrowing()
    {
        var query = SearchQuery.Parse("abc\uD800def");

        Assert.NotNull(query.Text);
    }

    [Theory]
    [Trait("Spec", "SRC-3")]
    [InlineData("' OR 1=1 --", new[] { "or", "1", "1" })]
    [InlineData("<script>alert(1)</script>", new[] { "script", "alert", "1", "script" })]
    [InlineData("& | ! ( ) :*", new string[0])]
    [InlineData("dress; DROP TABLE x", new[] { "dress", "drop", "table", "x" })]
    [InlineData("😀", new string[0])]
    public void Tokens_AreLettersAndDigitsOnly(string raw, string[] expected) =>
        Assert.Equal(expected, SearchQuery.Parse(raw).Tokens);

    [Fact]
    [Trait("Spec", "SRC-3")]
    public void Tokens_AreCappedInCountAndLength()
    {
        var query = SearchQuery.Parse(string.Join(' ', Enumerable.Range(0, 30).Select(i => "w" + i)));
        var long40 = SearchQuery.Parse(new string('z', 90));

        Assert.Equal(SearchQuery.MaxTokens, query.Tokens.Count);
        Assert.All(long40.Tokens, t => Assert.True(t.Length <= SearchQuery.MaxTokenLength));
    }

    [Theory]
    [Trait("Spec", "SRC-5")]
    [InlineData("ROMP-FLR-RED-2Y", "romp-flr-red-2y")]
    [InlineData("  romp-flr-red-2y  ", "romp-flr-red-2y")]
    [InlineData("FLR001", "flr001")]
    public void Sku_IsTrimmedAndLowerCased(string raw, string expected) =>
        Assert.Equal(expected, SkuQuery.TryParse(raw));

    [Theory]
    [Trait("Spec", "SRC-3")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("two words")]
    [InlineData("romp--x")]
    [InlineData("-romp")]
    [InlineData("' OR 1=1 --")]
    [InlineData("<script>")]
    public void Sku_RejectsTextThatIsNotCodeShaped(string? raw) =>
        Assert.Null(SkuQuery.TryParse(raw));

    [Theory]
    [Trait("Spec", "SRC-13")]
    [InlineData("a@b.com")]
    [InlineData("call 0300 1234567")]
    [InlineData("03001234567")]
    [InlineData("+92 300 123-4567")]
    [InlineData("https://evil.example/x")]
    [InlineData("www.example.com")]
    [InlineData("")]
    public void Miss_IsDroppedWhenItLooksPersonal(string raw) =>
        Assert.Null(MissRedactor.ForStorage(SearchQuery.Parse(raw)));

    [Theory]
    [Trait("Spec", "SRC-13")]
    [InlineData("Dungree", "dungree")]
    [InlineData("size 2 dress", "size 2 dress")]
    [InlineData("kurta 12345", "kurta 12345")]
    public void Miss_IsKeptNormalisedWhenHarmless(string raw, string expected) =>
        Assert.Equal(expected, MissRedactor.ForStorage(SearchQuery.Parse(raw)));

    private static SynonymExpansion Synonyms() => new(
    [
        ["frock", "dress", "gown"],
        ["shalwar kameez", "shalwar qameez", "salwar kameez"],
    ]);

    [Fact]
    [Trait("Spec", "SRC-12")]
    public void Expand_SingleWordGroup_AddsTheOtherTerms()
    {
        var text = TsQueryText.From(Synonyms().Expand(SearchQuery.Parse("frock")));

        Assert.Equal("(frock:* | dress:* | gown:*)", text);
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public void Expand_EitherTermFindsTheSameSet()
    {
        var fromDress = TsQueryText.From(Synonyms().Expand(SearchQuery.Parse("dress")));

        Assert.Contains("frock:*", fromDress);
        Assert.Contains("gown:*", fromDress);
    }

    [Fact]
    [Trait("Spec", "SRC-12")]
    public void Expand_Phrase_IsMatchedBeforeTokensAndKeepsWordOrder()
    {
        var text = TsQueryText.From(Synonyms().Expand(SearchQuery.Parse("red salwar kameez")));

        Assert.Equal("red:* & ((salwar:* <-> kameez:*) | (shalwar:* <-> kameez:*) | (shalwar:* <-> qameez:*))", text);
    }

    [Fact]
    [Trait("Spec", "SRC-3")]
    public void TsQuery_ForHostileInput_HoldsNoOperatorFromTheVisitor()
    {
        var text = TsQueryText.From(SynonymExpansion.None.Expand(SearchQuery.Parse("' OR 1=1 -- & | ! ( ) :*")));

        Assert.Equal("or:* & 1:* & 1:*", text);
    }

    [Fact]
    [Trait("Spec", "SRC-2")]
    public void TsQuery_ForNothing_IsNull() =>
        Assert.Null(TsQueryText.From(SynonymExpansion.None.Expand(SearchQuery.Parse("&&& ||"))));
}
