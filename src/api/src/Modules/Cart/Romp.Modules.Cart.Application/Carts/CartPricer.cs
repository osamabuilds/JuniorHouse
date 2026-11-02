using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Cart.Application.Carts;

/// <summary>
/// Prices a basket from the server's own data and checks live stock (CRT-1, 4, 7, 8; SEC-09). A fixed number of calls however many lines
/// there are: one bulk call to Catalog for prices and one bulk call to Warehouse for stock (no N+1). The cart itself holds only variant ids
/// and quantities, so nothing here can be influenced by what a client sent.
/// </summary>
public sealed class CartPricer(IVariantSalesQueries catalog, IStockAvailabilityQueries stock)
{
    public const string GoneName = "This item is no longer available";

    public async Task<PricedCart> PriceAsync(ShoppingCart? cart, CancellationToken cancellationToken)
    {
        if (cart is null || cart.IsEmpty)
        {
            return PricedCart.Empty;
        }

        var variantIds = cart.Lines.Select(l => l.VariantId).ToList();
        var sales = (await catalog.GetManyAsync(variantIds, cancellationToken)).ToDictionary(s => s.VariantId);

        var stockIds = sales.Values.Where(s => s.IsSellable && s.LabellingVariantId is not null).Select(s => s.LabellingVariantId!.Value).Distinct().ToList();
        var available = stockIds.Count == 0
            ? new Dictionary<long, int>()
            : (await stock.GetManyAsync(stockIds, cancellationToken)).ToDictionary(a => a.VariantId, a => a.Available);

        var lines = cart.Lines.OrderBy(l => l.Id == 0 ? long.MaxValue : l.Id).Select(line => Price(line, sales.GetValueOrDefault(line.VariantId), available)).ToList();
        var subtotal = lines.Where(l => !l.Issues.HasFlag(LineIssues.Unavailable)).Sum(l => l.LineTotal);
        return new PricedCart(lines, lines.Sum(l => l.Quantity), subtotal);
    }

    private static PricedLine Price(CartLine line, VariantSale? sale, Dictionary<long, int> available)
    {
        if (sale is null || !sale.IsSellable || sale.Price is not > 0)
        {
            // Never silently dropped: the shopper sees the line, why it cannot be bought, and can remove it (CRT-5, CRT-7).
            return new PricedLine(
                line.VariantId, sale?.ProductId ?? 0, sale?.ProductName ?? GoneName, sale?.Path ?? string.Empty, sale?.ColourName ?? string.Empty,
                sale?.SizeName ?? string.Empty, null, null, null, line.Quantity, 0m, 0, null, LineIssues.Unavailable,
                sale?.ImageKey, sale?.ImageAlt, sale?.ImageWidth, sale?.ImageHeight);
        }

        var onHand = sale.LabellingVariantId is { } labelling && available.TryGetValue(labelling, out var count) ? count : 0;
        var flags = LineIssues.None;
        decimal? previous = null;
        if (line.PriceSeen is { } seen && seen != sale.Price)
        {
            flags |= LineIssues.PriceChanged;
            previous = seen;
        }

        if (onHand < line.Quantity)
        {
            flags |= LineIssues.LowStock;
        }

        return new PricedLine(
            line.VariantId, sale.ProductId, sale.ProductName, sale.Path, sale.ColourName, sale.SizeName, sale.SkuCode, sale.Price, sale.CompareAtPrice,
            line.Quantity, decimal.Round(sale.Price.Value * line.Quantity, 2, MidpointRounding.AwayFromZero), onHand, previous, flags,
            sale.ImageKey, sale.ImageAlt, sale.ImageWidth, sale.ImageHeight);
    }
}
