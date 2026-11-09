namespace Romp.Modules.Customers.Domain.Wishlist;

/// <summary>The notes shown beside a saved product (WSL-5, WSL-6). Combinable, except <see cref="Unavailable"/> which stands alone.</summary>
[Flags]
public enum WishlistNotes
{
    None = 0,
    PriceDropped = 1,
    OnSale = 2,
    SoldOut = 4,
    Unavailable = 8,
}

/// <summary>Turns what the storefront read model says now, plus the price saved with the item, into the flags. Pure: no I/O.</summary>
public static class WishlistFlags
{
    /// <param name="savedPrice">The price recorded when the product was saved.</param>
    /// <param name="currentPrice">The current selling price, or null when the product is no longer published.</param>
    /// <param name="wasPrice">The struck-through price when the product is on sale.</param>
    /// <param name="isAvailable">Whether any variant is in stock, or null when the product is no longer published.</param>
    public static WishlistNotes Evaluate(decimal savedPrice, decimal? currentPrice, decimal? wasPrice, bool? isAvailable)
    {
        if (currentPrice is null)
        {
            return WishlistNotes.Unavailable;
        }

        var flags = WishlistNotes.None;
        if (currentPrice < savedPrice)
        {
            flags |= WishlistNotes.PriceDropped;
        }

        if (wasPrice is { } was && was > currentPrice)
        {
            flags |= WishlistNotes.OnSale;
        }

        if (isAvailable == false)
        {
            flags |= WishlistNotes.SoldOut;
        }

        return flags;
    }
}
