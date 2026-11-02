using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Domain.Carts;

namespace Romp.Modules.Cart.Application.Carts;

/// <summary>The caller's basket, priced and stock-checked. An owner with no basket, or an expired guest basket, gets an empty one (CRT-10).</summary>
public sealed record GetCartQuery(CartOwner Owner) : IRequest<PricedCart>;

public sealed class GetCartQueryHandler(ICartDbContext dbContext, CartPricer pricer, TimeProvider clock)
    : IRequestHandler<GetCartQuery, PricedCart>
{
    public async Task<PricedCart> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var cart = await CartStore.FindAsync(dbContext.Carts.AsNoTracking(), request.Owner, cancellationToken);
        if (cart is null || cart.IsExpired(clock.GetUtcNow()))
        {
            return PricedCart.Empty;
        }

        return await pricer.PriceAsync(cart, cancellationToken);
    }
}

/// <summary>The one place a basket is found by its owner, so every query and command filters the same way (SEC-05, CRT-9).</summary>
internal static class CartStore
{
    public static Task<ShoppingCart?> FindAsync(IQueryable<ShoppingCart> carts, CartOwner owner, CancellationToken cancellationToken)
    {
        var query = carts.Include(c => c.Lines);
        return owner.CustomerId is { } customerId
            ? query.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            : query.FirstOrDefaultAsync(c => c.GuestKeyHash == owner.GuestKeyHash, cancellationToken);
    }
}
