using MediatR;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Fulfilment.Application.Allocations;
using Romp.Modules.Fulfilment.Infrastructure.Persistence;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Fulfilment.Infrastructure.Allocations;

// The one place Orders' events are translated into Fulfilment commands (the anti-corruption mapper, ADR 0025): Fulfilment never looks at an Orders
// type outside this file. Each consumer sits behind the inbox, so a redelivered message is a no-op (ALC-5, ALC-11).

/// <summary>A cash on delivery order was confirmed: it enters the fulfilment queue (D-2, ALC-1).</summary>
internal sealed class OrderConfirmedFulfilmentConsumer(ISender sender, FulfDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<OrderConfirmedEvent>
{
    public async Task HandleAsync(OrderConfirmedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "CreateFulfilmentOrderOnOrderConfirmed", cancellationToken))
        {
            await sender.Send(new CreateFulfilmentOrderCommand(domainEvent.OrderId), cancellationToken);
        }
    }
}

/// <summary>An online order was paid: it enters the fulfilment queue (D-2, ALC-2).</summary>
internal sealed class OrderPaidFulfilmentConsumer(ISender sender, FulfDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<OrderPaidEvent>
{
    public async Task HandleAsync(OrderPaidEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "CreateFulfilmentOrderOnOrderPaid", cancellationToken))
        {
            await sender.Send(new CreateFulfilmentOrderCommand(domainEvent.OrderId), cancellationToken);
        }
    }
}

/// <summary>An order was cancelled: its allocation is released before pick, or a put-back task is raised after (ALC-6, ALC-7).</summary>
internal sealed class OrderCancelledFulfilmentConsumer(ISender sender, FulfDbContext dbContext, InboxGuard guard) : IOutboxMessageHandler<OrderCancelledEvent>
{
    public async Task HandleAsync(OrderCancelledEvent domainEvent, CancellationToken cancellationToken)
    {
        if (await guard.TryBeginAsync(dbContext, "CancelFulfilmentOrderOnOrderCancelled", cancellationToken))
        {
            await sender.Send(new CancelFulfilmentOrderCommand(domainEvent.OrderId), cancellationToken);
        }
    }
}
