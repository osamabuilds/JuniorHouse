using Romp.BuildingBlocks.Domain;
using Romp.Modules.Orders.Domain.Orders;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class OrderNumberTests
{
    [Fact]
    [Trait("Spec", "ORD-5")]
    public void ANumber_HasThePrefixYearFiveDigitSequenceAndACheckDigit()
    {
        var number = OrderNumbers.Format(2026, 123);

        Assert.StartsWith("RMP-2026-00123-", number, StringComparison.Ordinal);
        Assert.Equal(16, number.Length);
        Assert.True(OrderNumbers.IsValid(number));
    }

    [Fact]
    [Trait("Spec", "ORD-5")]
    public void AKnownNumber_HasTheKnownCheckDigit()
    {
        // Luhn over 202600001: the digit that completes it.
        Assert.Equal("RMP-2026-00001-4", OrderNumbers.Format(2026, 1));
    }

    [Fact]
    [Trait("Spec", "ORD-5")]
    public void EverySingleDigitTypo_IsCaught()
    {
        var number = OrderNumbers.Format(2026, 4567);
        var digitPositions = Enumerable.Range(0, number.Length).Where(i => char.IsAsciiDigit(number[i]));

        foreach (var i in digitPositions)
        {
            foreach (var d in "0123456789".Where(d => d != number[i]))
            {
                var typo = number.Remove(i, 1).Insert(i, d.ToString());
                Assert.False(OrderNumbers.IsValid(typo), typo);
            }
        }
    }

    [Theory]
    [Trait("Spec", "ORD-5")]
    [InlineData("")]
    [InlineData("RMP-2026-00001")]
    [InlineData("ABC-2026-00001-4")]
    [InlineData("RMP-26-00001-4")]
    [InlineData("RMP-2026-00001-48")]
    [InlineData("RMP-2026-0000x-8")]
    public void MalformedText_IsNotValid(string text)
    {
        Assert.False(OrderNumbers.IsValid(text));
    }

    [Fact]
    [Trait("Spec", "ORD-5")]
    public void Normalising_TrimsAndUpperCases_AndReturnsNullForABadNumber()
    {
        Assert.Equal("RMP-2026-00001-4", OrderNumbers.Normalise("  rmp-2026-00001-4 "));
        Assert.Null(OrderNumbers.Normalise("rmp-2026-00001-1"));
        Assert.Null(OrderNumbers.Normalise(null));
    }

    [Fact]
    [Trait("Spec", "ORD-5")]
    public void AnOutOfRangeYearOrSequence_IsRefused()
    {
        Assert.Throws<DomainException>(() => OrderNumbers.Format(1999, 1));
        Assert.Throws<DomainException>(() => OrderNumbers.Format(2026, 0));
    }
}
