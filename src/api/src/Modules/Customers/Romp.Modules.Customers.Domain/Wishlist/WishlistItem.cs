using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Customers.Domain.Wishlist;

/// <summary>
/// One product a customer saved (WSL-1). The customer id is set from the signed-in customer and never changes (SEC-05); the product id is the
/// Catalog product id the storefront read model exposes; the saved price is taken server-side from the read model, never from the client, and
/// is only used to flag a later price drop (WSL-6).
/// </summary>
public sealed class WishlistItem : Entity<long>, IAuditable
{
    private WishlistItem()
    {
        InsrBy = string.Empty;
    }

    public long CustomerId { get; private set; }

    public long ProductId { get; private set; }

    public decimal SavedPrice { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public static WishlistItem Create(long customerId, long productId, decimal savedPrice)
    {
        if (customerId <= 0)
        {
            throw new ArgumentException("A customer is required.", nameof(customerId));
        }

        if (productId <= 0)
        {
            throw new ArgumentException("A product is required.", nameof(productId));
        }

        if (savedPrice < 0)
        {
            throw new ArgumentException("The saved price cannot be negative.", nameof(savedPrice));
        }

        return new WishlistItem { CustomerId = customerId, ProductId = productId, SavedPrice = decimal.Round(savedPrice, 2) };
    }
}

/// <summary>The size rule of one customer's list (WSL-4): saving a product already on the list never counts against the limit.</summary>
public static class WishlistList
{
    public static void EnsureRoom(int currentCount, bool alreadySaved, int maxItems)
    {
        if (!alreadySaved && currentCount >= maxItems)
        {
            throw new DomainException($"Your wishlist holds up to {maxItems} products. Remove one to save another.");
        }
    }
}
