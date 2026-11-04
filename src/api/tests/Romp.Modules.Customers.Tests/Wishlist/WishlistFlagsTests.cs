using Romp.Modules.Customers.Domain.Wishlist;

namespace Romp.Modules.Customers.Tests.Wishlist;

/// <summary>customers/wishlist W1 (WSL-5, WSL-6): the notes shown beside a saved product, as a pure function.</summary>
public sealed class WishlistFlagsTests
{
    [Fact]
    [Trait("Spec", "WSL-6")]
    public void Evaluate_WhenCurrentPriceIsBelowSavedPrice_FlagsPriceDropped()
    {
        var flags = WishlistFlags.Evaluate(savedPrice: 2000m, currentPrice: 1500m, wasPrice: null, isAvailable: true);

        Assert.Equal(WishlistNotes.PriceDropped, flags);
    }

    [Fact]
    [Trait("Spec", "WSL-6")]
    public void Evaluate_WhenPriceIsUnchangedOrHigher_FlagsNothing()
    {
        Assert.Equal(WishlistNotes.None, WishlistFlags.Evaluate(2000m, 2000m, null, true));
        Assert.Equal(WishlistNotes.None, WishlistFlags.Evaluate(2000m, 2500m, null, true));
    }

    [Fact]
    [Trait("Spec", "WSL-6")]
    public void Evaluate_WhenWasPriceIsAboveCurrentPrice_FlagsOnSale()
    {
        var flags = WishlistFlags.Evaluate(1500m, 1500m, wasPrice: 2000m, isAvailable: true);

        Assert.Equal(WishlistNotes.OnSale, flags);
    }

    [Fact]
    [Trait("Spec", "WSL-6")]
    public void Evaluate_WhenDroppedAndOnSale_FlagsBoth()
    {
        var flags = WishlistFlags.Evaluate(2000m, 1500m, 2000m, true);

        Assert.Equal(WishlistNotes.PriceDropped | WishlistNotes.OnSale, flags);
    }

    [Fact]
    [Trait("Spec", "WSL-6")]
    public void Evaluate_WhenNotAvailable_FlagsSoldOut()
    {
        var flags = WishlistFlags.Evaluate(2000m, 2000m, null, isAvailable: false);

        Assert.Equal(WishlistNotes.SoldOut, flags);
    }

    [Fact]
    [Trait("Spec", "WSL-5")]
    public void Evaluate_WhenProductIsNoLongerPublished_FlagsOnlyUnavailable()
    {
        var flags = WishlistFlags.Evaluate(2000m, currentPrice: null, wasPrice: null, isAvailable: null);

        Assert.Equal(WishlistNotes.Unavailable, flags);
    }
}
