using MediatR;
using Romp.Modules.Cart.Application.DiscountCodes;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Cart.Infrastructure.Carts;

namespace Romp.Modules.Cart.Infrastructure.DiscountCodes;

/// <summary>The Cart side of <see cref="IDiscountRedemptions"/>: the caller's own basket from the session or guest cookie, never from an id.</summary>
internal sealed class DiscountRedemptions(CartOwnerAccessor owners, ISender sender) : IDiscountRedemptions
{
    public async Task<DiscountHold> HoldForCurrentCartAsync(string placementKey, string? guestMobileHash, CancellationToken cancellationToken)
    {
        var owner = await owners.ReadAsync(cancellationToken);
        return owner is not { } current
            ? new DiscountHold(false, null, 0m, null)
            : await sender.Send(new HoldRedemptionCommand(current, placementKey, guestMobileHash), cancellationToken);
    }

    public Task ReleaseAsync(string placementKey, CancellationToken cancellationToken) =>
        sender.Send(new ReleaseRedemptionCommand(placementKey), cancellationToken);
}
