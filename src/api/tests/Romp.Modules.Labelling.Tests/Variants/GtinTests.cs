using Romp.BuildingBlocks.Domain;
using Romp.Modules.Labelling.Domain.Variants;

namespace Romp.Modules.Labelling.Tests.Variants;

public sealed class GtinTests
{
    // Published, real-world GTIN-13s (check digits from GS1 examples / well-known products).
    [Theory]
    [InlineData("4006381333931")]
    [InlineData("5901234123457")]
    [InlineData("9780201379624")]
    [Trait("Spec", "VAR-1")]
    public void TryParse_AcceptsPublishedValidGtins(string value)
    {
        Assert.True(Gtin.TryParse(value, out var gtin));
        Assert.Equal(value, gtin!.Value);
    }

    [Theory]
    [InlineData("4006381333932", "wrong check digit")]
    [InlineData("400638133393", "12 digits")]
    [InlineData("40063813339311", "14 digits")]
    [InlineData("40063813339A1", "non-digit")]
    [InlineData("", "empty")]
    [InlineData(null, "null")]
    [InlineData("４００６３８１３３３９３１", "full-width digits")]
    [Trait("Spec", "VAR-14")]
    public void TryParse_RejectsAnythingThatIsNotAValidGtin13(string? value, string why)
    {
        Assert.False(Gtin.TryParse(value, out _), why);
        Assert.Throws<DomainException>(() => Gtin.Parse(value));
    }

    [Theory]
    [InlineData("400638133393", '1')]
    [InlineData("590123412345", '7')]
    [InlineData("978020137962", '4')]
    [InlineData("000000000000", '0')]
    public void CheckDigit_MatchesTheGs1ModuloTenRule(string payload, char expected) =>
        Assert.Equal(expected, Gtin.CheckDigit(payload));

    [Fact]
    [Trait("Spec", "VAR-1")]
    public void Create_BuildsPrefixPlusZeroPaddedReferencePlusCheckDigit()
    {
        var prefix = CompanyPrefix.Parse("0200000");

        var gtin = Gtin.Create(prefix, 42);

        Assert.Equal(13, gtin.Value.Length);
        Assert.StartsWith("0200000", gtin.Value, StringComparison.Ordinal);
        Assert.Equal("00042", gtin.Value.Substring(7, 5));
        Assert.True(Gtin.TryParse(gtin.Value, out _));
        Assert.Equal(gtin.Value[..12], gtin.Payload);
    }

    [Fact]
    [Trait("Spec", "VAR-12")]
    public void Create_RefusesAReferenceBeyondThePrefixCapacity()
    {
        var prefix = CompanyPrefix.Parse("02000000"); // 8 digits => 10,000 numbers

        Assert.Equal(10_000, prefix.Capacity);
        Assert.NotNull(Gtin.Create(prefix, 9_999));
        Assert.Throws<DomainException>(() => Gtin.Create(prefix, 10_000));
        Assert.Throws<DomainException>(() => Gtin.Create(prefix, -1));
    }

    [Theory]
    [InlineData("0200000", true, 100_000)]
    [InlineData("02000000", true, 10_000)]
    [InlineData("627123456", false, 1_000)]
    [Trait("Spec", "VAR-11")]
    public void CompanyPrefix_ReportsTestRangeAndCapacity(string value, bool isTest, long capacity)
    {
        var prefix = CompanyPrefix.Parse(value);

        Assert.Equal(isTest, prefix.IsTestPrefix);
        Assert.Equal(capacity, prefix.Capacity);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("1234567890")]
    [InlineData("12345a7")]
    [InlineData("")]
    [InlineData(null)]
    [Trait("Spec", "VAR-11")]
    public void CompanyPrefix_RejectsWrongLengthOrNonDigits(string? value)
    {
        Assert.False(CompanyPrefix.TryParse(value, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }
}
