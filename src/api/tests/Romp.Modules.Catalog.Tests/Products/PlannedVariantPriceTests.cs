using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Domain.Products;

namespace Romp.Modules.Catalog.Tests.Products;

public sealed class PlannedVariantPriceTests
{
    private static PlannedVariant NewVariant(decimal? price = null) =>
        Product.Create(1, "Dress", "A dress", 2, 3, 4, "dress", [1], [4], price).Variants.Single();

    [Fact]
    [Trait("Spec", "PRD-2")]
    [Trait("Spec", "D4")]
    public void CompareAtPrice_MustBeHigherThanThePrice()
    {
        var variant = NewVariant();

        Assert.Throws<DomainException>(() => variant.SetPrice(1000m, 1000m, "Test Person"));
        Assert.Throws<DomainException>(() => variant.SetPrice(1000m, 900m, "Test Person"));
        Assert.NotNull(variant.SetPrice(1000m, 1200m, "Test Person"));
        Assert.Equal(1200m, variant.CompareAtPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    [InlineData(10_000_000)]
    [Trait("Spec", "PRD-8")]
    public void Price_MustBePositiveAndWithinTheLimit(double price) =>
        Assert.Throws<DomainException>(() => NewVariant().SetPrice((decimal)price, null, "Test Person"));

    [Fact]
    [Trait("Spec", "PRD-8")]
    public void Price_IsRoundedToTwoDecimals_AsMoney()
    {
        var variant = NewVariant();

        variant.SetPrice(1299.995m, null, "Test Person");

        Assert.Equal(1300.00m, variant.Price);
    }

    [Fact]
    [Trait("Spec", "PRD-7")]
    public void AChangedPrice_ReturnsOneHistoryRow_WithOldNewAndWho()
    {
        var variant = NewVariant(1000m);

        var entry = variant.SetPrice(1100m, 1500m, "Ayesha Khan");

        Assert.NotNull(entry);
        Assert.Equal(1000m, entry.OldPrice);
        Assert.Equal(1100m, entry.NewPrice);
        Assert.Null(entry.OldCompareAtPrice);
        Assert.Equal(1500m, entry.NewCompareAtPrice);
        Assert.Equal("Ayesha Khan", entry.ChangedBy);
    }

    [Fact]
    [Trait("Spec", "PRD-7")]
    public void SettingTheSamePriceAgain_RecordsNothing()
    {
        var variant = NewVariant(1000m);

        Assert.Null(variant.SetPrice(1000m, null, "Test Person"));
    }

    [Fact]
    [Trait("Spec", "PRD-8")]
    public void ANewProduct_IgnoresAnUnusableDefaultPrice()
    {
        Assert.Null(NewVariant(0m).Price);
        Assert.Null(NewVariant(-5m).Price);
        Assert.Equal(1299m, NewVariant(1299m).Price);
    }
}
