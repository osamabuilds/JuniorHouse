using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Returns.Application.ReverseQc;
using Romp.Modules.Returns.Infrastructure.Persistence;
using Romp.Modules.Shipping.Contracts;

namespace Romp.Modules.Returns.Infrastructure.ReverseQc;

/// <summary>
/// A parcel came back to origin: a return case is opened for its order (RQC-1, FR-SC-18). Every other status is ignored. Behind the inbox, so the same
/// event twice is the same event; the case is also unique per order in the database.
/// </summary>
internal sealed class ShipmentReturnedConsumer(ISender sender, RtrnDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<ShipmentStatusChangedEvent>
{
    public async Task HandleAsync(ShipmentStatusChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.StatusCode != ShipmentStatusCodes.ReturnedToOrigin)
        {
            return;
        }

        if (await guard.TryBeginAsync(dbContext, "OpenReturnCaseOnShipmentReturned", cancellationToken))
        {
            await sender.Send(new OpenReturnCaseCommand(domainEvent.OrderId, domainEvent.OrderNumber), cancellationToken);
        }
    }
}
