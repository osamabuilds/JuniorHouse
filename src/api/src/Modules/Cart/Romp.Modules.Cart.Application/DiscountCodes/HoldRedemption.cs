using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Contracts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Domain.DiscountCodes;
using Romp.Modules.Cart.Domain.Totals;

namespace Romp.Modules.Cart.Application.DiscountCodes;

/// <summary>
/// Keeps the basket's code for an order being placed (ADR 0025). The code row is locked first and its uses are counted after, so concurrent
/// holds are serialised and the cap holds exactly (DSC-10). A repeated key returns the earlier hold unchanged.
/// </summary>
public sealed record HoldRedemptionCommand(CartOwner Owner, string PlacementKey, string? GuestMobileHash) : IRequest<DiscountHold>, ICartCommand;

public sealed class HoldRedemptionCommandHandler(ICartDbContext dbContext, CartPricer pricer, DiscountService discounts, TimeProvider clock)
    : IRequestHandler<HoldRedemptionCommand, DiscountHold>
{
    public async Task<DiscountHold> Handle(HoldRedemptionCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var existing = await dbContext.Redemptions.FirstOrDefaultAsync(r => r.PlacementKey == request.PlacementKey, cancellationToken);
        if (existing is not null)
        {
            var known = await discounts.FindByIdAsync(existing.DiscountCodeId, cancellationToken);
            return existing.Status == RedemptionStatus.Released
                ? new DiscountHold(false, known?.Code, 0m, "That code is no longer held for this order.")
                : new DiscountHold(true, known?.Code, existing.Amount, null);
        }

        var cart = await CartStore.FindAsync(dbContext.Carts.AsNoTracking(), request.Owner, cancellationToken);
        if (cart?.AppliedCodeId is not { } codeId || cart.IsExpired(now))
        {
            return new DiscountHold(false, null, 0m, null);
        }

        await dbContext.LockDiscountCodeAsync(codeId, cancellationToken);
        var code = await discounts.FindByIdAsync(codeId, cancellationToken);
        var customerKey = request.Owner.CustomerId is not null
            ? DiscountService.CustomerKey(request.Owner)
            : string.IsNullOrWhiteSpace(request.GuestMobileHash) ? null : $"M{request.GuestMobileHash}";
        var priced = await pricer.PriceAsync(cart, cancellationToken);
        var result = await discounts.EvaluateAsync(code, priced, now, customerKey, cancellationToken);
        if (!result.IsAccepted)
        {
            return new DiscountHold(false, code?.Code, 0m, DiscountMessages.For(result));
        }

        var amount = OrderTotals.Round(result.Amount);
        dbContext.Redemptions.Add(new Redemption(codeId, request.PlacementKey, customerKey ?? "G", amount));
        return new DiscountHold(true, code!.Code, amount, null);
    }
}

/// <summary>Gives a held code's place back (the order failed or was never created). Confirmed uses are untouched here: cancellation has its own path.</summary>
public sealed record ReleaseRedemptionCommand(string PlacementKey) : IRequest, ICartCommand;

public sealed class ReleaseRedemptionCommandHandler(ICartDbContext dbContext) : IRequestHandler<ReleaseRedemptionCommand>
{
    public async Task Handle(ReleaseRedemptionCommand request, CancellationToken cancellationToken)
    {
        var held = await dbContext.Redemptions.FirstOrDefaultAsync(r => r.PlacementKey == request.PlacementKey && r.Status == RedemptionStatus.Held, cancellationToken);
        held?.Release();
    }
}

/// <summary>
/// Releases holds whose order never arrived within <c>Cart:HoldMinutes</c>, so a crashed placement cannot keep a code's last use for ever
/// (ADR 0025). Returns how many were released.
/// </summary>
public sealed record SweepHeldRedemptionsCommand : IRequest<int>, ICartCommand;

public sealed class SweepHeldRedemptionsCommandHandler(ICartDbContext dbContext, TimeProvider clock, IOptions<CartOptions> options)
    : IRequestHandler<SweepHeldRedemptionsCommand, int>
{
    public async Task<int> Handle(SweepHeldRedemptionsCommand request, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddMinutes(-options.Value.HoldMinutes);
        var stale = await dbContext.Redemptions.Where(r => r.Status == RedemptionStatus.Held && r.InsrDte < cutoff).ToListAsync(cancellationToken);
        foreach (var redemption in stale)
        {
            redemption.Release();
        }

        return stale.Count;
    }
}
