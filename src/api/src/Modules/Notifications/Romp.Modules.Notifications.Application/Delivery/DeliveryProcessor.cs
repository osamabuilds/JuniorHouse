using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Application.Delivery;

/// <summary>
/// One pass of the delivery worker: claim due deliveries, build each message from the order as it is now, send, and record the result
/// (DLV-2, DLV-8, DLV-9, DLV-12). Sending happens outside any database transaction; a crash between sending and saving leaves the lease to run
/// out, so a duplicate is possible but rare (ADR 0026). A failure of one delivery never stops the others.
/// </summary>
public sealed class DeliveryProcessor(
    INotificationsDbContext dbContext,
    IDeliveryClaimer claimer,
    IOrderNotificationQueries orders,
    IMessageRenderer renderer,
    IEnumerable<IChannelSender> senders,
    DeliverySettings settings,
    TimeProvider clock)
{
    private readonly RetryPolicy _policy = settings.ToRetryPolicy();

    /// <summary>Returns how many deliveries were handled in this pass.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var ids = await claimer.ClaimAsync(clock.GetUtcNow(), settings.ClaimBatchSize, TimeSpan.FromSeconds(settings.LeaseSeconds), cancellationToken);
        if (ids.Count == 0)
        {
            return 0;
        }

        // One query for every claimed delivery with its notification (no N+1); the order is read per notification through the contract.
        var notifications = await dbContext.Notifications
            .Include(n => n.Deliveries)
            .Where(n => n.Deliveries.Any(d => ids.Contains(d.Id)))
            .ToListAsync(cancellationToken);

        var handled = 0;
        foreach (var notification in notifications)
        {
            foreach (var delivery in notification.Deliveries.Where(d => ids.Contains(d.Id)))
            {
                await ProcessAsync(notification, delivery, cancellationToken);
                handled++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return handled;
    }

    private async Task ProcessAsync(Notification notification, ChannelDelivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.Status != DeliveryStatus.Queued)
        {
            return;
        }

        // Too late to help the customer (a COD notice three days on): record it and stop (DLV-10).
        if (notification.IsExpired(clock.GetUtcNow()))
        {
            delivery.Expire();
            dbContext.Attempts.Add(new DeliveryAttempt(delivery.Id, delivery.AttemptCount, DeliveryStatus.Expired, null, clock.GetUtcNow()));
            return;
        }

        var sender = senders.FirstOrDefault(s => s.Channel == delivery.Channel);
        if (sender is null)
        {
            // Nothing can carry this channel yet: wait without using up an attempt (CHN-4).
            Fail(notification, delivery, ErrorCategory.CircuitOpen);
            return;
        }

        try
        {
            var order = await orders.FindAsync(notification.ReferenceId, cancellationToken);
            if (order is null)
            {
                Fail(notification, delivery, ErrorCategory.Temporary);
                return;
            }

            var recipient = delivery.Channel == NotificationChannel.Email ? order.ContactEmail : order.ContactMobile;
            if (string.IsNullOrWhiteSpace(recipient))
            {
                Fail(notification, delivery, ErrorCategory.NoAddress);
                return;
            }

            var rendered = renderer.Render(notification.Kind, delivery.Channel, order);
            var providerId = await sender.SendAsync(
                new OutboundMessage(delivery.Channel, recipient, rendered.Subject, rendered.Text, rendered.Html), cancellationToken);

            delivery.MarkSent(providerId);
            dbContext.Attempts.Add(new DeliveryAttempt(delivery.Id, delivery.AttemptCount, DeliveryStatus.Sent, null, clock.GetUtcNow()));
        }
        catch (ChannelSendException ex)
        {
            Fail(notification, delivery, ex.Category);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Anything else from a provider is treated as temporary. The message is not kept: it may carry an address (SEC-25).
            Fail(notification, delivery, ErrorCategory.Temporary);
        }
    }

    private void Fail(Notification notification, ChannelDelivery delivery, ErrorCategory category)
    {
        var now = clock.GetUtcNow();
        delivery.RecordFailure(category, now, _policy, (Random.Shared.NextDouble() * 2) - 1);
        dbContext.Attempts.Add(new DeliveryAttempt(delivery.Id, delivery.AttemptCount, delivery.Status, category, now));

        // Out of attempts: keep the references and history for a resend, and raise the alert in the same save (DLV-3, DLV-12).
        if (delivery.Status == DeliveryStatus.Dead)
        {
            dbContext.DeadLetters.Add(new DeadLetter(notification.Id, delivery.Id, notification.Kind, delivery.Channel, category, delivery.AttemptCount, now));
        }
    }
}
