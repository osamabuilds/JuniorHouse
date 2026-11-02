using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Application.DiscountCodes;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Domain.Totals;

namespace Romp.Modules.Cart.Application.Totals;

/// <summary>
/// A basket priced line by line, together with what the shopper pays: subtotal, discount, delivery and total (TOT-1 to TOT-7).
/// <paramref name="DiscountCode"/> is the code taking money off, if one holds; <paramref name="DiscountNotice"/> says a code no longer holds.
/// </summary>
public sealed record CartWithTotals(PricedCart Cart, OrderAmounts Amounts, string? DiscountCode = null, string? DiscountNotice = null);

/// <summary>
/// Works out the money for a priced basket with the one pricing routine (<see cref="OrderTotals"/>): the same code the cart, checkout and the
/// order use, so no second copy can drift (SEC-09). Lines that cannot be bought are left out; delivery waits for a city (TOT-4).
/// </summary>
public sealed class CartTotalsCalculator(ShippingPolicyLoader policies)
{
    public async Task<OrderAmounts> CalculateAsync(PricedCart cart, int? cityId, bool isCashOnDelivery, decimal discount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cart);

        var lines = cart.Lines
            .Where(l => !l.Issues.HasFlag(LineIssues.Unavailable) && l.UnitPrice is not null)
            .Select(l => new TotalLine(l.UnitPrice!.Value, l.Quantity))
            .ToList();

        // First pass: the subtotal and the discount actually applied, because free delivery depends on the subtotal after the discount.
        var preview = OrderTotals.Calculate(lines, discount, shippingFee: null);
        if (preview.Subtotal <= 0)
        {
            return preview;
        }

        var policy = await policies.LoadAsync(cancellationToken);
        var fee = policy.FeeFor(cityId, preview.Subtotal - preview.Discount, isCashOnDelivery);
        return OrderTotals.Calculate(lines, discount, fee);
    }
}

/// <summary>The caller's basket with its totals. <paramref name="CityId"/> is chosen on the order page; with none, delivery is "calculated at checkout".</summary>
public sealed record GetCartWithTotalsQuery(CartOwner? Owner, int? CityId, bool IsCashOnDelivery) : IRequest<CartWithTotals>;

public sealed class GetCartWithTotalsQueryHandler(
    ICartDbContext dbContext, CartPricer pricer, CartTotalsCalculator totals, DiscountService discounts, TimeProvider clock)
    : IRequestHandler<GetCartWithTotalsQuery, CartWithTotals>
{
    public async Task<CartWithTotals> Handle(GetCartWithTotalsQuery request, CancellationToken cancellationToken)
    {
        PricedCart priced = PricedCart.Empty;
        AppliedDiscount applied = new(null, 0m, null);
        if (request.Owner is { } owner)
        {
            var now = clock.GetUtcNow();
            var cart = await CartStore.FindAsync(dbContext.Carts.AsNoTracking(), owner, cancellationToken);
            if (cart is not null && !cart.IsExpired(now))
            {
                priced = await pricer.PriceAsync(cart, cancellationToken);
                applied = await DiscountOutcome.ResolveAsync(discounts, cart, priced, now, DiscountService.CustomerKey(owner), cancellationToken);
            }
        }

        var amounts = await totals.CalculateAsync(priced, request.CityId, request.IsCashOnDelivery, applied.Amount, cancellationToken);
        return new CartWithTotals(priced, amounts, applied.Code, applied.Notice);
    }
}
