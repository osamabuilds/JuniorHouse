using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Infrastructure.Delivery;
using Romp.Modules.Notifications.Application.Channels;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Infrastructure.Channels;

/// <summary>Carries the WhatsApp channel of the delivery worker over whichever <see cref="IWhatsAppSender"/> is registered.</summary>
internal sealed class WhatsAppChannelSender(IWhatsAppSender whatsApp) : IChannelSender
{
    public NotificationChannel Channel => NotificationChannel.WhatsApp;

    public async Task<string?> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        var id = await whatsApp.SendAsync(
            new WhatsAppMessage(message.Recipient, message.Text, message.Buttons ?? [], message.ProviderTemplate, message.Parameters), cancellationToken);
        return string.IsNullOrEmpty(id) ? null : id;
    }
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

/// <summary>The daily send count as one atomic upsert on <c>NTFY_DAY_CNT</c>, so concurrent workers never lose a count (CHN-15).</summary>
internal sealed class SqlDailySendCounter(Persistence.NtfyDbContext dbContext, TimeProvider clock) : IDailySendCounter
{
    private const string UpsertSql = """
        INSERT INTO "NTFY"."NTFY_DAY_CNT" ("DAY_DT", "CHNL_ID", "SENT_CNT", "INSR_DTE", "INSR_BY")
        VALUES ({0}, {1}, 1, {2}, 'delivery-worker')
        ON CONFLICT ("DAY_DT", "CHNL_ID")
        DO UPDATE SET "SENT_CNT" = "NTFY_DAY_CNT"."SENT_CNT" + 1, "UPDT_DTE" = {2}, "UPDT_BY" = 'delivery-worker'
        """;

    public async Task IncrementAsync(DateOnly day, NotificationChannel channel, CancellationToken cancellationToken) =>
        await dbContext.Database.ExecuteSqlRawAsync(UpsertSql, [day, DeliveryConversions.ChannelId(channel), clock.GetUtcNow()], cancellationToken);

    public async Task<int> CountAsync(DateOnly day, NotificationChannel channel, CancellationToken cancellationToken) =>
        await dbContext.DailyCounts.AsNoTracking().Where(c => c.Day == day && c.Channel == channel).Select(c => c.SentCount).SingleOrDefaultAsync(cancellationToken);
}
