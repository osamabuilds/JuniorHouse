using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Domain.DiscountCodes;

namespace Romp.Modules.Cart.Application.Carts;

/// <summary>
/// The order exists (ADR 0025 step 7): the held discount redemption becomes a confirmed use and the basket the order was made from is
/// emptied. Both steps are repeatable: a redelivered event changes nothing. <paramref name="CartRef"/> is the basket id the order was priced
/// from; an unknown or already empty basket is fine.
/// </summary>
public sealed record ConfirmOrderPlacedCommand(string PlacementKey, string OrderNumber, string? CartRef) : IRequest, ICartCommand;

public sealed class ConfirmOrderPlacedCommandHandler(ICartDbContext dbContext, TimeProvider clock, IOptions<CartOptions> options) : IRequestHandler<ConfirmOrderPlacedCommand>
{
    public async Task Handle(ConfirmOrderPlacedCommand request, CancellationToken cancellationToken)
    {
        var redemption = await dbContext.Redemptions.FirstOrDefaultAsync(r => r.PlacementKey == request.PlacementKey, cancellationToken);
        redemption?.Confirm(request.OrderNumber);

        if (long.TryParse(request.CartRef, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var cartId))
        {
            var cart = await dbContext.Carts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == cartId, cancellationToken);
            if (cart is { IsEmpty: false } or { AppliedCodeId: not null })
            {
                cart.Clear(clock.GetUtcNow(), options.Value.GuestLifetime);
            }
        }
    }
}
