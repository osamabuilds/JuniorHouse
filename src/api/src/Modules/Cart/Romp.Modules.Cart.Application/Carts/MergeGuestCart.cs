using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Customers.Contracts;

namespace Romp.Modules.Cart.Application.Carts;

/// <summary>
/// Hands a guest's basket to the customer who has just signed in or registered (CRT-3, FR-35). <paramref name="GuestKey"/> is the raw
/// <c>romp-guest</c> cookie value the sign-in read; a value that is not one we issued does nothing.
/// </summary>
public sealed record MergeGuestCartCommand(long CustomerId, string GuestKey) : IRequest, ICartCommand;

/// <summary>
/// A variant in both baskets becomes one line with the quantities added (capped at the per-line maximum), nothing is lost or duplicated, and
/// the guest basket is deleted. The merge does not cut a line down to the stock on hand: a line that now asks for more than is available is
/// flagged "low stock" with the available count on the next read, so the shopper decides (the pricer already does that for every basket).
/// </summary>
public sealed class MergeGuestCartCommandHandler(CartCommandSupport support, ICartDbContext dbContext) : IRequestHandler<MergeGuestCartCommand>
{
    public async Task Handle(MergeGuestCartCommand request, CancellationToken cancellationToken)
    {
        if (!CartOwner.TryHashGuestKey(request.GuestKey, out var hash))
        {
            return;
        }

        var guest = await CartStore.FindAsync(dbContext.Carts, CartOwner.ForGuest(hash), cancellationToken);
        if (guest is null)
        {
            return;
        }

        if (!guest.IsEmpty && !guest.IsExpired(support.Now))
        {
            var account = await support.FindOrCreateAsync(CartOwner.ForCustomer(request.CustomerId), cancellationToken);
            var options = support.Options;
            account.MergeFrom(guest, new Dictionary<long, int>(), options.MaxPerLine, options.MaxLines, support.Now, options.GuestLifetime);
        }

        dbContext.Carts.Remove(guest);
    }
}

/// <summary>Registered with the customer sign-in (<see cref="ICustomerSignedInHook"/>): a failing merge never stops a sign-in, and the guest basket simply waits.</summary>
public sealed class GuestCartMergeHook(ISender sender) : ICustomerSignedInHook
{
    public async Task OnSignedInAsync(SignedInContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.IsNullOrEmpty(context.GuestSessionId))
        {
            await sender.Send(new MergeGuestCartCommand(context.CustomerId, context.GuestSessionId), cancellationToken);
        }
    }
}
