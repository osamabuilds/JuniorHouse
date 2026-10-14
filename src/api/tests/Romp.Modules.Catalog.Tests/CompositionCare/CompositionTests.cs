using Romp.Modules.Catalog.Domain.CompositionCare;

namespace Romp.Modules.Catalog.Tests.CompositionCare;

public sealed class CompositionTests
{
    [Fact]
    [Trait("Spec", "CMP-7")]
    public void ThreeSharesOfThirtyThreePointThreeAndThirtyThreePointFour_TotalExactlyOneHundred()
    {
        var shares = new[] { 33.3m, 33.3m, 33.4m };

        Assert.Equal(100.0m, Composition.Total(shares));
        Assert.True(Composition.IsComplete(shares));
    }

    [Theory]
    [InlineData(99.9)]
    [InlineData(100.1)]
    [InlineData(95.0)]
    [Trait("Spec", "CMP-1")]
    public void ATotalOtherThanExactly100_IsIncomplete(double total) =>
        Assert.False(Composition.IsComplete([(decimal)total]));

    [Fact]
    [Trait("Spec", "CMP-1")]
    public void NoRowsAtAll_IsIncomplete() => Assert.False(Composition.IsComplete([]));

    [Fact]
    [Trait("Spec", "CMP-6")]
    public void TheSameFibreTwice_IsRefused() =>
        Assert.NotNull(Composition.Check([(1, 50m), (1, 50m)]));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.01)]
    [InlineData(101)]
    [Trait("Spec", "CMP-6")]
    public void APercentageOutsideZeroToOneHundred_IsRefused(double percent) =>
        Assert.NotNull(Composition.Check([(1, (decimal)percent)]));

    [Theory]
    [InlineData(95.55)]
    [InlineData(33.33)]
    [Trait("Spec", "CMP-7")]
    public void MoreThanOneDecimalPlace_IsRefused(double percent) =>
        Assert.Contains("one decimal", Composition.Check([(1, (decimal)percent)]), StringComparison.Ordinal);

    [Fact]
    public void ValidRows_AreAccepted() =>
        Assert.Null(Composition.Check([(1, 95m), (2, 5m)]));
}
