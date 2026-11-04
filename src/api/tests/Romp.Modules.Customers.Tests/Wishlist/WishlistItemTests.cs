using Romp.BuildingBlocks.Domain;
using Romp.Modules.Customers.Domain.Wishlist;

namespace Romp.Modules.Customers.Tests.Wishlist;

/// <summary>customers/wishlist W1 (WSL-1, WSL-4): one customer's item and the list limit.</summary>
public sealed class WishlistItemTests
{
    [Fact]
    [Trait("Spec", "WSL-1")]
    public void Create_StoresTheCustomerProductAndSavedPrice()
    {
        var item = WishlistItem.Create(customerId: 7, productId: 42, savedPrice: 1999.50m);

        Assert.Equal(7, item.CustomerId);
        Assert.Equal(42, item.ProductId);
        Assert.Equal(1999.50m, item.SavedPrice);
    }

    [Theory]
    [Trait("Spec", "WSL-1")]
    [InlineData(0, 1, 10)]
    [InlineData(1, 0, 10)]
    [InlineData(1, 1, -1)]
    public void Create_WithAnInvalidValue_Throws(long customerId, long productId, int price)
    {
        Assert.Throws<ArgumentException>(() => WishlistItem.Create(customerId, productId, price));
    }

    [Fact]
    [Trait("Spec", "WSL-4")]
    public void EnsureRoom_WhenTheProductIsAlreadySaved_AllowsItEvenAtTheLimit()
    {
        WishlistList.EnsureRoom(currentCount: 200, alreadySaved: true, maxItems: 200);
    }

    [Fact]
    [Trait("Spec", "WSL-4")]
    public void EnsureRoom_WhenFullAndTheProductIsNew_Throws()
    {
        var ex = Assert.Throws<DomainException>(() => WishlistList.EnsureRoom(200, alreadySaved: false, maxItems: 200));

        Assert.Contains("200", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "WSL-4")]
    public void EnsureRoom_BelowTheLimit_Allows()
    {
        WishlistList.EnsureRoom(199, alreadySaved: false, maxItems: 200);
    }
}
