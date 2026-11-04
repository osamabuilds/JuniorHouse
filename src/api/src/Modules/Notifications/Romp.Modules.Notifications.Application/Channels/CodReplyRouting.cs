using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Domain.Channels;
using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Application.Channels;

/// <summary>The button ids of the COD confirmation message. They travel in the message and come back in the reply.</summary>
public static class CodButtons
{
    public const string Confirm = "cod_confirm";
    public const string Cancel = "cod_cancel";
}

/// <summary>
/// Routes a customer's button reply to the COD outcome (CHN-12, COD-14 to COD-16). The reply is matched to the message it answers by the provider
/// message id of a WhatsApp delivery for a COD confirmation request; a reply that matches no sent message, or a button that is neither Confirm nor
/// Cancel, is ignored. Orders then checks the replying number against the order's mobile before changing anything.
/// </summary>
public sealed class CodReplyHandler(INotificationsDbContext dbContext, ICodCustomerResponses responses) : IWhatsAppInboundHandler
{
    public WhatsAppEventType EventType => WhatsAppEventType.ButtonReply;

    public async Task<long?> HandleAsync(WhatsAppInboundEvent inbound, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inbound);
        var confirm = inbound.Payload switch
        {
            CodButtons.Confirm => true,
            CodButtons.Cancel => false,
            _ => (bool?)null,
        };
        if (confirm is null || string.IsNullOrEmpty(inbound.ProviderMessageId))
        {
            return null;
        }

        var match = await (
            from delivery in dbContext.Deliveries.AsNoTracking()
            join notification in dbContext.Notifications.AsNoTracking() on delivery.NotificationId equals notification.Id
            where delivery.ProviderMessageId == inbound.ProviderMessageId
                && delivery.Channel == NotificationChannel.WhatsApp
                && notification.Kind == NotificationKind.CodConfirmationRequest
                && notification.ReferenceKind == ReferenceKinds.Order
            select new { DeliveryId = delivery.Id, OrderId = notification.ReferenceId })
            .FirstOrDefaultAsync(cancellationToken);
        if (match is null)
        {
            return null;
        }

        await responses.RecordReplyAsync(match.OrderId, inbound.SenderNumber, confirm.Value, cancellationToken);
        return match.DeliveryId;
    }
}
