using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Infrastructure.Persistence;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Infrastructure.Delivery;

/// <summary>
/// Shared body of the four Orders-event consumers: behind the inbox (one handling per outbox message), create the notification and its
/// deliveries, and save once, so the inbox row and the notification commit together (DLV-1, DLV-4, DLV-6). A redelivery finds the inbox
/// row and does nothing; a retry after a crash finds the notification by event id and kind and does nothing.
/// </summary>
internal static class OrderEventIntake
{
    public static async Task RunAsync(
        NotificationIntent? intent, string handlerName, NtfyDbContext dbContext, InboxGuard guard, CreateNotification create, CancellationToken cancellationToken)
    {
        if (intent is null || !await guard.TryBeginAsync(dbContext, handlerName, cancellationToken))
        {
            return;
        }

        await create.HandleAsync(intent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class OrderPlacedNotificationConsumer(NtfyDbContext dbContext, InboxGuard guard, CreateNotification create) : IOutboxMessageHandler<OrderPlacedEvent>
{
    public Task HandleAsync(OrderPlacedEvent domainEvent, CancellationToken cancellationToken) =>
        OrderEventIntake.RunAsync(OrderEventMapper.Map(domainEvent), "NotifyOnOrderPlaced", dbContext, guard, create, cancellationToken);
}

internal sealed class OrderPaidNotificationConsumer(NtfyDbContext dbContext, InboxGuard guard, CreateNotification create) : IOutboxMessageHandler<OrderPaidEvent>
{
    public Task HandleAsync(OrderPaidEvent domainEvent, CancellationToken cancellationToken) =>
        OrderEventIntake.RunAsync(OrderEventMapper.Map(domainEvent), "NotifyOnOrderPaid", dbContext, guard, create, cancellationToken);
}

internal sealed class OrderCancelledNotificationConsumer(NtfyDbContext dbContext, InboxGuard guard, CreateNotification create) : IOutboxMessageHandler<OrderCancelledEvent>
{
    public Task HandleAsync(OrderCancelledEvent domainEvent, CancellationToken cancellationToken) =>
        OrderEventIntake.RunAsync(OrderEventMapper.Map(domainEvent), "NotifyOnOrderCancelled", dbContext, guard, create, cancellationToken);
}

internal sealed class CodConfirmationRequestedNotificationConsumer(NtfyDbContext dbContext, InboxGuard guard, CreateNotification create) : IOutboxMessageHandler<CodConfirmationRequestedEvent>
{
    public Task HandleAsync(CodConfirmationRequestedEvent domainEvent, CancellationToken cancellationToken) =>
        OrderEventIntake.RunAsync(OrderEventMapper.Map(domainEvent), "NotifyOnCodConfirmationRequested", dbContext, guard, create, cancellationToken);
}
