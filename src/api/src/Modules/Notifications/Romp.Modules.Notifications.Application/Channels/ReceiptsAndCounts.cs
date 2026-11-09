using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Domain.Channels;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Application.Channels;

/// <summary>
/// Delivery receipts from the provider update the status of the WhatsApp delivery they are about (CHN-14, DLG-10): <c>delivered</c> and
/// <c>read</c> make it Delivered, <c>failed</c> marks it failed (the email, sent in parallel, stands), <c>sent</c> changes nothing. A receipt for no
/// message of ours is ignored. Email never gets a receipt, so it stays at Sent.
/// </summary>
public sealed class ReceiptHandler(INotificationsDbContext dbContext) : IWhatsAppInboundHandler
{
    public WhatsAppEventType EventType => WhatsAppEventType.Receipt;

    public async Task<long?> HandleAsync(WhatsAppInboundEvent inbound, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inbound);
        if (string.IsNullOrEmpty(inbound.ProviderMessageId))
        {
            return null;
        }

        var delivery = await dbContext.Deliveries.FirstOrDefaultAsync(
            d => d.ProviderMessageId == inbound.ProviderMessageId && d.Channel == NotificationChannel.WhatsApp, cancellationToken);
        if (delivery is null)
        {
            return null;
        }

        // A receipt can only move a message that was actually sent: a late or out-of-order one for anything else is ignored.
        if (delivery.Status is DeliveryStatus.Sent or DeliveryStatus.Delivered or DeliveryStatus.Failed)
        {
            switch (inbound.Payload)
            {
                case "delivered" or "read":
                    if (delivery.Status == DeliveryStatus.Sent)
                    {
                        delivery.MarkDelivered();
                    }

                    break;
                case "failed":
                    if (delivery.Status == DeliveryStatus.Sent)
                    {
                        delivery.MarkFailedByProvider();
                    }

                    break;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return delivery.Id;
    }
}

/// <summary>
/// Counts what is sent per day and channel so the business can watch the cost of WhatsApp (CHN-15). One atomic upsert, so two workers never lose
/// a count.
/// </summary>
public interface IDailySendCounter
{
    Task IncrementAsync(DateOnly day, NotificationChannel channel, CancellationToken cancellationToken);

    /// <summary>The number sent on <paramref name="day"/> over <paramref name="channel"/> (the delivery log's "WhatsApp sends today").</summary>
    Task<int> CountAsync(DateOnly day, NotificationChannel channel, CancellationToken cancellationToken);
}
