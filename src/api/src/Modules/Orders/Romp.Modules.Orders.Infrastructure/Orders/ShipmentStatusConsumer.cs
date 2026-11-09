using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Orders.Application.Orders;
using Romp.Modules.Orders.Infrastructure.Persistence;
using Romp.Modules.Shipping.Contracts;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>
/// A parcel moved at the courier: the order follows (OHS-4). The one place Shipping's events become an Orders command (the anti-corruption mapper,
/// ADR 0025); behind the inbox, so a redelivered message is a no-op.
/// </summary>
internal sealed class ShipmentStatusOrderConsumer(ISender sender, OrdrDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<ShipmentStatusChangedEvent>
{
    public async Task HandleAsync(ShipmentStatusChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "MoveOrderOnShipmentStatus", cancellationToken))
        {
            await sender.Send(new ApplyShipmentStatusCommand(domainEvent.OrderId, domainEvent.StatusCode), cancellationToken);
        }
    }
}
