using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Orders.Application.Abstractions;
using Romp.Modules.Orders.Domain.Orders;
using Romp.Modules.Shipping.Contracts;

namespace Romp.Modules.Orders.Application.Orders;

/// <summary>
/// The courier moved a parcel (OHS-4, TRK-5): the order becomes Dispatched when the courier collects it, and Delivered when it is handed over. The
/// move is recorded in the order history with the actor "system" and the reference "courier". A repeat changes nothing; a status that does not apply
/// to the order's state (an order already cancelled, say) is ignored, because Orders alone decides what its own status may be (ADR 0031).
/// </summary>
public sealed record ApplyShipmentStatusCommand(long OrderId, string StatusCode) : IRequest<bool>, IOrdersCommand;

public sealed class ApplyShipmentStatusCommandHandler(IOrdersDbContext dbContext, TimeProvider clock) : IRequestHandler<ApplyShipmentStatusCommand, bool>
{
    private const string CourierRef = "courier";

    public async Task<bool> Handle(ApplyShipmentStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        switch (request.StatusCode)
        {
            case ShipmentStatusCodes.PickedUp:
                return Move(order, OrderStatuses.Dispatched, now);
            case ShipmentStatusCodes.Delivered:
                // A parcel delivered before its pick-up was ever reported still passes through Dispatched, so the history stays whole.
                Move(order, OrderStatuses.Dispatched, now);
                return Move(order, OrderStatuses.Delivered, now);
            default:
                return false;
        }
    }

    private static bool Move(Order order, short to, DateTimeOffset now) =>
        order.MoveTo(to, ActorKinds.System, CourierRef, null, null, now) == OrderTransition.Changed;
}
