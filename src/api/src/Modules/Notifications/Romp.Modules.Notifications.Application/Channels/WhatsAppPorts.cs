using Romp.Modules.Notifications.Domain.Channels;

namespace Romp.Modules.Notifications.Application.Channels;

/// <summary>One WhatsApp message to send. Built at send time and never stored (SEC-25). <c>Buttons</c> are the quick replies (Confirm, Cancel) of a COD request.</summary>
public sealed record WhatsAppMessage(string RecipientNumber, string Text, IReadOnlyList<WhatsAppButton> Buttons);

public sealed record WhatsAppButton(string Id, string Title);


/// <summary>A verified inbound event, reduced to what routing needs. The provider message id ties it back to the delivery it answers.</summary>
public sealed record WhatsAppInboundEvent(string EventId, WhatsAppEventType Type, string SenderNumber, string? ProviderMessageId, string? Payload);

/// <summary>
/// Sends one WhatsApp message (CHN-8). The real Cloud API adapter and the fake both sit behind this port. Throws
/// <see cref="Delivery.ChannelSendException"/> on failure and returns the provider's message id.
/// </summary>
public interface IWhatsAppSender
{
    Task<string> SendAsync(WhatsAppMessage message, CancellationToken cancellationToken);
}
