using MediatR;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Cart.Application.Abstractions;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Domain.Carts;
using Romp.Modules.Cart.Domain.DiscountCodes;

namespace Romp.Modules.Cart.Application.DiscountCodes;

/// <summary>
/// Apply a code to the caller's basket, on the shopper's explicit action only (FR-25). A refusal carries a specific inline reason (FR-26);
/// the same reason is given for an unknown, mistyped or deactivated code (DSC-9). A second code is refused, never swapped in silently (DSC-4).
/// </summary>
public sealed record ApplyDiscountCommand(CartOwner Owner, string Code) : IRequest, ICartCommand;

/// <summary>Take the applied code off the basket.</summary>
public sealed record RemoveDiscountCommand(CartOwner Owner) : IRequest, ICartCommand;

public sealed class ApplyDiscountCommandHandler(ICartDbContext dbContext, CartPricer pricer, DiscountService discounts, TimeProvider clock, Microsoft.Extensions.Options.IOptions<CartOptions> options)
    : IRequestHandler<ApplyDiscountCommand>
{
    public async Task Handle(ApplyDiscountCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var cart = await CartStore.FindAsync(dbContext.Carts, request.Owner, cancellationToken);
        if (cart is null || cart.IsEmpty || cart.IsExpired(now))
        {
            throw Refuse("Add something to your bag before applying a code.");
        }

        var code = await discounts.FindAsync(request.Code, cancellationToken);
        if (cart.AppliedCodeId is { } existingId && code is not null && existingId != code.Id)
        {
            var existing = await discounts.FindByIdAsync(existingId, cancellationToken);
            throw Refuse(DiscountMessages.For(DiscountResult.Rejected(DiscountRejection.SecondCode), existing?.Code));
        }

        var priced = await pricer.PriceAsync(cart, cancellationToken);
        var result = await discounts.EvaluateAsync(code, priced, now, DiscountService.CustomerKey(request.Owner), cancellationToken);
        if (!result.IsAccepted)
        {
            throw Refuse(DiscountMessages.For(result));
        }

        cart.ApplyCode(code!.Id, now, options.Value.GuestLifetime);
    }

    private static ValidationException Refuse(string message) => new(new Dictionary<string, string[]> { ["code"] = [message] });
}

public sealed class RemoveDiscountCommandHandler(ICartDbContext dbContext, TimeProvider clock, Microsoft.Extensions.Options.IOptions<CartOptions> options)
    : IRequestHandler<RemoveDiscountCommand>
{
    public async Task Handle(RemoveDiscountCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartStore.FindAsync(dbContext.Carts, request.Owner, cancellationToken);
        cart?.RemoveCode(clock.GetUtcNow(), options.Value.GuestLifetime);
    }
}
