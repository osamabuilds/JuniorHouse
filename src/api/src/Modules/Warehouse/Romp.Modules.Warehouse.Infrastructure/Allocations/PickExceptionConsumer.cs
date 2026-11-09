using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Fulfilment.Contracts;
using Romp.Modules.Warehouse.Application.Allocations;
using Romp.Modules.Warehouse.Infrastructure.Persistence;

namespace Romp.Modules.Warehouse.Infrastructure.Allocations;

/// <summary>
/// A picker flagged a stock mismatch: the bin is marked "under review" so no order is allocated from it until staff have counted it (PEX-6).
/// Stock is never overwritten here: correcting it is a normal, audited adjustment (BRD section 5.14). Behind the inbox, so a redelivery is a no-op.
/// </summary>
internal sealed class PickExceptionBinFlagConsumer(ISender sender, WhseDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<PickExceptionRaisedEvent>
{
    public async Task HandleAsync(PickExceptionRaisedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "FlagBinOnPickException", cancellationToken))
        {
            await sender.Send(new SetBinReviewCommand(domainEvent.BinId, "PICK_EXCEPTION"), cancellationToken);
        }
    }
}
