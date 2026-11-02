using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Orders.Contracts;
using Romp.Modules.Warehouse.Application.Reservations;
using Romp.Modules.Warehouse.Infrastructure.Persistence;

namespace Romp.Modules.Warehouse.Infrastructure.Reservations;

/// <summary>
/// The one place Orders' events are translated into Warehouse commands (the anti-corruption mapper, ADR 0025): Warehouse speaks in
/// placement keys and order numbers only, and never looks at an Orders type outside this file.
/// </summary>
internal static class OrderEventMapper
{
    public static ConfirmReservationCommand Map(OrderPlacedEvent placed) => new(placed.PlacementKey, placed.OrderNumber);

    public static ReleaseReservationCommand Map(OrderCancelledEvent cancelled) => new(cancelled.PlacementKey, ReservationReasons.Released);
}

/// <summary>An order was placed: confirm its stock hold. Behind the inbox, so a redelivered message is a no-op.</summary>
internal sealed class OrderPlacedReservationConsumer(ISender sender, WhseDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<OrderPlacedEvent>
{
    public async Task HandleAsync(OrderPlacedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "ConfirmReservationOnOrderPlaced", cancellationToken))
        {
            await sender.Send(OrderEventMapper.Map(domainEvent), cancellationToken);
        }
    }
}

/// <summary>An order was cancelled: give its stock back, once.</summary>
internal sealed class OrderCancelledReservationConsumer(ISender sender, WhseDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<OrderCancelledEvent>
{
    public async Task HandleAsync(OrderCancelledEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "ReleaseReservationOnOrderCancelled", cancellationToken))
        {
            await sender.Send(OrderEventMapper.Map(domainEvent), cancellationToken);
        }
    }
}
