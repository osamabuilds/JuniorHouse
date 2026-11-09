using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Cart.Application.Carts;
using Romp.Modules.Cart.Application.DiscountCodes;
using Romp.Modules.Cart.Infrastructure.Persistence;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Cart.Infrastructure.DiscountCodes;

/// <summary>The one place Orders' events become Cart commands (anti-corruption mapper, ADR 0025): Cart speaks in placement keys, order numbers and basket ids.</summary>
internal static class OrderEventMapper
{
    public static ConfirmOrderPlacedCommand Map(OrderPlacedEvent placed) => new(placed.PlacementKey, placed.OrderNumber, placed.CartRef);

    public static ReleaseRedemptionCommand Map(OrderCancelledEvent cancelled) => new(cancelled.PlacementKey);
}

/// <summary>An order was placed: confirm the discount use and empty the basket. Behind the inbox.</summary>
internal sealed class OrderPlacedCartConsumer(ISender sender, CartDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<OrderPlacedEvent>
{
    public async Task HandleAsync(OrderPlacedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "ConfirmCartOnOrderPlaced", cancellationToken))
        {
            await sender.Send(OrderEventMapper.Map(domainEvent), cancellationToken);
        }
    }
}

/// <summary>An order was cancelled: the code can be used again. Behind the inbox.</summary>
internal sealed class OrderCancelledCartConsumer(ISender sender, CartDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<OrderCancelledEvent>
{
    public async Task HandleAsync(OrderCancelledEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "ReleaseRedemptionOnOrderCancelled", cancellationToken))
        {
            await sender.Send(OrderEventMapper.Map(domainEvent), cancellationToken);
        }
    }
}
