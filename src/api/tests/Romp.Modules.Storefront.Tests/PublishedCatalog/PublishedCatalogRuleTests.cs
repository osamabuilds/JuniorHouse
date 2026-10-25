using System.Text;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Version-wins: a snapshot replaces the stored state only when it is newer (published-catalog task 8; PCT-5).</summary>
public sealed class VersionedSnapshotTests
{
    [Theory]
    [Trait("Spec", "PCT-5")]
    [InlineData(1, null, true)]
    [InlineData(5, 4L, true)]
    [InlineData(5, 5L, false)]
    [InlineData(4, 5L, false)]
    public void ASnapshotReplacesTheStoredOne_OnlyWhenItIsNewer(long incoming, long? stored, bool expected) =>
        Assert.Equal(expected, VersionedSnapshot.Replaces(incoming, stored));

    [Theory]
    [Trait("Spec", "PCT-5")]
    [InlineData(0, null)]
    [InlineData(-3, null)]
    [InlineData(0, 4L)]
    public void AVersionBelowOne_NeverReplacesAnything(long incoming, long? stored) =>
        Assert.False(VersionedSnapshot.Replaces(incoming, stored));
}

/// <summary>What a shopper is told about one variant (PCT-3, PCT-9, PCT-11, Q-PCT-2).</summary>
public sealed class AvailabilityTests
{
    private const int Threshold = 5;

    [Theory]
    [Trait("Spec", "PCT-3")]
    [InlineData(0, AvailabilityClass.SoldOut)]
    [InlineData(-2, AvailabilityClass.SoldOut)]
    [InlineData(1, AvailabilityClass.LowStock)]
    [InlineData(5, AvailabilityClass.LowStock)]
    [InlineData(6, AvailabilityClass.InStock)]
    [InlineData(500, AvailabilityClass.InStock)]
    public void TheClassFollowsTheQuantityAndTheThreshold(int available, AvailabilityClass expected) =>
        Assert.Equal(expected, Availability.Classify(sellable: true, available, Threshold));

    [Fact]
    [Trait("Spec", "PCT-3")]
    public void AThresholdOfZero_MeansThereIsNoLowStockBand()
    {
        Assert.Equal(AvailabilityClass.InStock, Availability.Classify(true, 1, 0));
        Assert.Equal(AvailabilityClass.SoldOut, Availability.Classify(true, 0, 0));
    }

    [Fact]
    [Trait("Spec", "PCT-9")]
    public void AVariantWithNoStockRecord_OrThatIsNotSellable_IsNotYetAvailable_NotAFailure()
    {
        Assert.Equal(AvailabilityClass.NotYetAvailable, Availability.Classify(sellable: true, available: null, Threshold));
        Assert.Equal(AvailabilityClass.NotYetAvailable, Availability.Classify(sellable: false, available: 40, Threshold));
        Assert.Equal(AvailabilityClass.NotYetAvailable, Availability.Classify(sellable: false, available: null, Threshold));
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    public void ANegativeThreshold_IsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Availability.Classify(true, 3, -1));

    [Theory]
    [Trait("Spec", "PCT-11")]
    [InlineData(AvailabilityClass.InStock, true)]
    [InlineData(AvailabilityClass.LowStock, true)]
    [InlineData(AvailabilityClass.SoldOut, false)]
    [InlineData(AvailabilityClass.NotYetAvailable, false)]
    public void OnlyInStockAndLowStock_CanBeBought(AvailabilityClass value, bool canBuy) =>
        Assert.Equal(canBuy, value.IsPurchasable());
}

/// <summary>A product is listable only with something to buy and a ready main image; its page is visible whenever it is published (D-2).</summary>
public sealed class ProductVisibilityTests
{
    [Fact]
    [Trait("Spec", "PCT-3")]
    public void AProductWithAnInStockVariantAndAReadyImage_IsListable()
    {
        var result = ProductVisibility.Evaluate([AvailabilityClass.SoldOut, AvailabilityClass.LowStock], primaryImageReady: true);

        Assert.True(result.IsInStock);
        Assert.True(result.IsListable);
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void AnInStockProduct_WhoseMainImageIsStillBeingMade_WaitsOffTheListings()
    {
        var result = ProductVisibility.Evaluate([AvailabilityClass.InStock], primaryImageReady: false);

        Assert.True(result.IsInStock);
        Assert.False(result.IsListable);
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    public void AProductWithNothingToBuy_IsNotInStock_AndNotListable()
    {
        var soldOut = ProductVisibility.Evaluate([AvailabilityClass.SoldOut, AvailabilityClass.NotYetAvailable], primaryImageReady: true);
        var empty = ProductVisibility.Evaluate([], primaryImageReady: true);

        Assert.All([soldOut, empty], r => Assert.False(r.IsInStock || r.IsListable));
    }
}

/// <summary>The price a listing shows and the sale flag (PCT-4; Q-LST-2: Sale = an in-stock variant whose compare-at price is above its price).</summary>
public sealed class PriceSummaryTests
{
    private static PricedVariant V(decimal? price, decimal? compareAt, AvailabilityClass availability) => new(price, compareAt, availability);

    [Fact]
    [Trait("Spec", "PCT-4")]
    public void TheRange_IsTheLowestAndHighestPriceOfTheVariantsThatCanBeBought()
    {
        var summary = PriceSummary.Of([
            V(1299m, null, AvailabilityClass.InStock),
            V(1499m, null, AvailabilityClass.LowStock),
            V(999m, null, AvailabilityClass.SoldOut),
            V(1m, null, AvailabilityClass.NotYetAvailable)]);

        Assert.NotNull(summary);
        Assert.Equal((1299m, 1499m, false, 0m), (summary.MinPrice, summary.MaxPrice, summary.OnSale, summary.MaxDiscountPercent));
    }

    [Fact]
    [Trait("Spec", "PCT-4")]
    public void WhenEverythingIsSoldOut_ThePageStillShowsThePricesOfTheVariantsThatExist()
    {
        var summary = PriceSummary.Of([V(1299m, null, AvailabilityClass.SoldOut), V(1599m, null, AvailabilityClass.SoldOut), V(5m, null, AvailabilityClass.NotYetAvailable)]);

        Assert.Equal((1299m, 1599m), (summary!.MinPrice, summary.MaxPrice));
    }

    [Fact]
    [Trait("Spec", "PCT-4")]
    public void NoPricedVariant_MeansNoSummary()
    {
        Assert.Null(PriceSummary.Of([]));
        Assert.Null(PriceSummary.Of([V(null, null, AvailabilityClass.InStock), V(0m, null, AvailabilityClass.InStock), V(900m, null, AvailabilityClass.NotYetAvailable)]));
    }

    [Fact]
    [Trait("Spec", "PCT-4")]
    public void AnInStockVariantWithACompareAtAbovePrice_PutsTheProductOnSale_WithTheBiggestSaving()
    {
        var summary = PriceSummary.Of([
            V(800m, 1000m, AvailabilityClass.InStock),
            V(750m, 1500m, AvailabilityClass.LowStock),
            V(1000m, null, AvailabilityClass.InStock)]);

        Assert.True(summary!.OnSale);
        Assert.Equal(50m, summary.MaxDiscountPercent);
    }

    [Fact]
    [Trait("Spec", "PCT-4")]
    public void ASaleOnlyOnASoldOutVariant_DoesNotPutAnInStockProductOnSale()
    {
        var summary = PriceSummary.Of([V(900m, null, AvailabilityClass.InStock), V(500m, 1000m, AvailabilityClass.SoldOut)]);

        Assert.False(summary!.OnSale);
        Assert.Equal(0m, summary.MaxDiscountPercent);
    }

    [Fact]
    [Trait("Spec", "PCT-4")]
    public void ACompareAtThatIsNotAbovePrice_IsNotASale_AndTheSavingIsRoundedToTwoPlaces()
    {
        Assert.False(PriceSummary.Of([V(1000m, 1000m, AvailabilityClass.InStock)])!.OnSale);
        Assert.False(PriceSummary.Of([V(1000m, 900m, AvailabilityClass.InStock)])!.OnSale);
        Assert.Equal(33.33m, PriceSummary.Of([V(2000m, 3000m, AvailabilityClass.InStock)])!.MaxDiscountPercent);
    }
}

/// <summary>Derivative file names are generated, never taken from a client, and carry a content hash so they can be cached for ever (PCT-7, PCT-10).</summary>
public sealed class DerivativeNameTests
{
    private static readonly string Hash = DerivativeName.HashOf("original image bytes"u8);

    [Fact]
    [Trait("Spec", "PCT-10")]
    public void TheHash_IsThirtyTwoLowercaseHexCharacters_AndFollowsTheContent()
    {
        Assert.Matches("^[0-9a-f]{32}$", Hash);
        Assert.Equal(Hash, DerivativeName.HashOf(Encoding.UTF8.GetBytes("original image bytes")));
        Assert.NotEqual(Hash, DerivativeName.HashOf("a changed image"u8));
    }

    [Theory]
    [Trait("Spec", "PCT-7")]
    [InlineData(DerivativeFormat.Webp, 640, "webp", "WEBP")]
    [InlineData(DerivativeFormat.Jpeg, 1280, "jpg", "JPG")]
    public void TheName_IsHashWidthAndExtension(DerivativeFormat format, int width, string extension, string code)
    {
        var name = DerivativeName.For(Hash, width, format);

        Assert.Equal($"{Hash}-{width}.{extension}", name);
        Assert.Equal(code, format.Code());
    }

    [Fact]
    [Trait("Spec", "PCT-10")]
    public void AChangedImage_GetsNewNames_SoTheOldOnesCanBeRemovedLater()
    {
        var before = DerivativeName.For(Hash, 640, DerivativeFormat.Webp);
        var after = DerivativeName.For(DerivativeName.HashOf("a changed image"u8), 640, DerivativeFormat.Webp);

        Assert.NotEqual(before, after);
    }

    [Theory]
    [Trait("Spec", "PCT-7")]
    [InlineData("")]
    [InlineData("../../etc/passwd")]
    [InlineData("ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("0123456789abcdef")]
    public void AHashThatIsNotOurs_IsRefused(string hash) =>
        Assert.Throws<ArgumentException>(() => DerivativeName.For(hash, 640, DerivativeFormat.Webp));

    [Theory]
    [Trait("Spec", "PCT-7")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8193)]
    public void AWidthOutsideOneToEightThousand_IsRefused(int width) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DerivativeName.For(Hash, width, DerivativeFormat.Webp));

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void ANameThatWasMade_CanBeReadBack()
    {
        var name = DerivativeName.For(Hash, 960, DerivativeFormat.Jpeg);

        Assert.True(DerivativeName.TryParse(name, out var hash, out var width, out var format));
        Assert.Equal((Hash, 960, DerivativeFormat.Jpeg), (hash, width, format));
    }

    [Theory]
    [Trait("Spec", "PCT-7")]
    [InlineData("")]
    [InlineData("../0123456789abcdef0123456789abcdef-640.webp")]
    [InlineData("0123456789abcdef0123456789abcdef-640.png")]
    [InlineData("0123456789abcdef0123456789abcdef-0.webp")]
    [InlineData("0123456789abcdef0123456789abcdef-640.webp/extra")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF-640.webp")]
    public void AnythingThatIsNotAGeneratedName_DoesNotParse(string candidate) =>
        Assert.False(DerivativeName.TryParse(candidate, out _, out _, out _));
}
