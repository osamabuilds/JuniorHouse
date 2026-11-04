using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Customers.Application.NotifyMe;
using Romp.Modules.Customers.Infrastructure.Persistence;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Customers.Infrastructure.NotifyMe;

/// <summary>The one place Notify Me reads Warehouse's event: it takes the variant and the quantity and nothing else (ADR 0002).</summary>
public static class StockChangedMapper
{
    public static ProcessStockChangeCommand ToCommand(StockLevelChangedEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return new ProcessStockChangeCommand(domainEvent.VariantId, domainEvent.Available);
    }
}

/// <summary>
/// Reacts to a stock change (ADR 0012). Behind the inbox, so a redelivered message notifies nobody twice (NTM-8). The command closes the
/// requests and queues the emails in one transaction; a failure throws and the dispatcher retries the whole message.
/// </summary>
public sealed class StockChangedConsumer(ISender sender, CustDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<StockLevelChangedEvent>
{
    public async Task HandleAsync(StockLevelChangedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (!await guard.TryBeginAsync(dbContext, "NotifyOnStockChanged", cancellationToken))
        {
            return;
        }

        await sender.Send(StockChangedMapper.ToCommand(domainEvent), cancellationToken);
    }
}
