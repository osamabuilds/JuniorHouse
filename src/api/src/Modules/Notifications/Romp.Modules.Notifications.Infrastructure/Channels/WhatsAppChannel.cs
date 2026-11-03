using System.Collections.Concurrent;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Infrastructure.Channels;

/// <summary>Carries the WhatsApp channel of the delivery worker over whichever <see cref="IWhatsAppSender"/> is registered.</summary>
internal sealed class WhatsAppChannelSender(IWhatsAppSender whatsApp) : IChannelSender
{
    public NotificationChannel Channel => NotificationChannel.WhatsApp;

    public async Task<string?> SendAsync(OutboundMessage message, CancellationToken cancellationToken) =>
        await whatsApp.SendAsync(new WhatsAppMessage(message.Recipient, message.Text, []), cancellationToken);
}

/// <summary>
/// The stand-in until the business has a WhatsApp number and Meta's approval: records what would be sent, can be told to fail, and can raise
/// inbound events so every path is testable (CHN-8). Registered as a singleton so a test and the worker see the same record.
/// </summary>
public sealed class FakeWhatsAppSender : IWhatsAppSender
{
    private readonly ConcurrentQueue<WhatsAppMessage> _sent = new();
    private readonly ConcurrentQueue<ErrorCategory> _failures = new();
    private long _counter;

    /// <summary>Raised by <see cref="RaiseInbound"/>; the inbound handling of H5/H6 subscribes to it in tests.</summary>
    public event Action<WhatsAppInboundEvent>? InboundReceived;

    public IReadOnlyList<WhatsAppMessage> Sent => [.. _sent];

    /// <summary>The next sends fail, one category per send, before sends succeed again.</summary>
    public void FailNext(ErrorCategory category, int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            _failures.Enqueue(category);
        }
    }

    public void RaiseInbound(WhatsAppInboundEvent inbound) => InboundReceived?.Invoke(inbound);

    public Task<string> SendAsync(WhatsAppMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_failures.TryDequeue(out var category))
        {
            throw new ChannelSendException(category, "The fake WhatsApp sender was told to fail.");
        }

        _sent.Enqueue(message);
        return Task.FromResult($"fake-wamid-{Interlocked.Increment(ref _counter)}");
    }
}
