using MediatR;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Application.Totals;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Cart.Infrastructure.Carts;

namespace Romp.Modules.Cart.Infrastructure.Totals;

/// <summary>The Cart side of <see cref="ICheckoutPricing"/>: the caller's basket, priced and totalled by the one routine (TOT-3, ORD-4).</summary>
internal sealed class CheckoutPricing(CartOwnerAccessor owners, ISender sender) : ICheckoutPricing
{
    public async Task<string?> GetCallerKeyAsync(CancellationToken cancellationToken)
    {
        var owner = await owners.ReadAsync(cancellationToken);
        return owner is not { } o ? null : o.CustomerId is { } id ? $"C{id}" : $"G{o.GuestKeyHash}";
    }

    public async Task<Contracts.CheckoutPricing> PriceCurrentCartAsync(int? cityId, bool isCashOnDelivery, CancellationToken cancellationToken)
    {
        var owner = await owners.ReadAsync(cancellationToken);
        var result = await sender.Send(new GetCartWithTotalsQuery(owner, cityId, isCashOnDelivery), cancellationToken);

        var problems = new List<string>();
        if (result.Cart.IsEmpty)
        {
            problems.Add("Your bag is empty.");
        }

        foreach (var line in result.Cart.Lines.Where(l => !l.IsOrderable))
        {
            problems.Add(line.Issues.HasFlag(LineIssues.Unavailable)
                ? $"{line.Name} is no longer available."
                : line.Available <= 0 ? $"{line.Name} is out of stock." : $"Only {line.Available} of {line.Name} are in stock.");
        }

        var lines = result.Cart.Lines.Where(l => l.IsOrderable && l.UnitPrice is not null).Select(l => new CheckoutLine(
            l.VariantId, l.ProductId, l.Name, l.Path, l.Sku, l.Colour, l.Size, l.UnitPrice!.Value, l.Quantity, l.LineTotal)).ToList();
        var a = result.Amounts;
        return new Contracts.CheckoutPricing(
            lines, a.Subtotal, a.Discount, a.Shipping, a.ShippingKnown, a.Total, result.Cart.CartId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            problems.Count == 0, problems);
    }
}
