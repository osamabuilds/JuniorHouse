using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Cart.Domain.Carts;

/// <summary>
/// One variant and a quantity. A line never stores a price, a name or a stock figure: all of those are read fresh each time the cart is
/// priced (D-2, SEC-09), so a price change is always picked up and nothing the client sends can be trusted or kept.
/// </summary>
public sealed class CartLine : Entity<long>, IAuditable
{
    private CartLine()
    {
        InsrBy = string.Empty;
    }

    internal CartLine(long variantId, int quantity)
    {
        VariantId = variantId;
        Quantity = quantity;
        InsrBy = string.Empty;
    }

    public long CartId { get; private set; }

    public long VariantId { get; private set; }

    public int Quantity { get; internal set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; }

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
