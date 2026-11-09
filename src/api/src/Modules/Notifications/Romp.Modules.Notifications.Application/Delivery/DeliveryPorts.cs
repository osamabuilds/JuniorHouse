using Romp.Modules.Notifications.Domain.Delivery;
using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Notifications.Application.Delivery;

/// <summary>What a channel sender is asked to send. Built at send time from the order; never stored (SEC-25).</summary>
public sealed record OutboundMessage(
    NotificationChannel Channel,
    string Recipient,
    string Subject,
    string Text,
    string? Html,
    string? ProviderTemplate = null,
    IReadOnlyList<string>? Parameters = null,
    IReadOnlyList<Channels.WhatsAppButton>? Buttons = null);

/// <summary>A channel failed. The category decides what happens next: retry on the schedule, fail fast, or wait for the circuit (DLV-8, CHN-4).</summary>
public sealed class ChannelSendException(ErrorCategory category, string message) : Exception(message)
{
    public ErrorCategory Category { get; } = category;
}

/// <summary>Sends one message over one channel. Throws <see cref="ChannelSendException"/> on failure; returns the provider's message id, if it gives one.</summary>
public interface IChannelSender
{
    NotificationChannel Channel { get; }

    Task<string?> SendAsync(OutboundMessage message, CancellationToken cancellationToken);
}

/// <summary>Builds the words of a message from the order as stored now (OCF-5). Every value is encoded for the channel (CHN-9, OCF-7).</summary>
public interface IMessageRenderer
{
    Task<RenderedMessage> RenderAsync(NotificationKind kind, NotificationChannel channel, OrderNotificationView order, CancellationToken cancellationToken);
}

/// <summary>The words of a message. For WhatsApp it also names the approved provider template, the values for its placeholders and the reply buttons (CHN-13).</summary>
public sealed record RenderedMessage(
    string Subject,
    string Text,
    string? Html,
    string? ProviderTemplate = null,
    IReadOnlyList<string>? Parameters = null,
    IReadOnlyList<Channels.WhatsAppButton>? Buttons = null);

/// <summary>
/// Takes due deliveries for one worker (DLV-9). Implemented in one statement with <c>FOR UPDATE SKIP LOCKED</c> and a lease, so two workers
/// never hold the same delivery and a crashed worker's lease simply runs out.
/// </summary>
public interface IDeliveryClaimer
{
    Task<IReadOnlyList<long>> ClaimAsync(DateTimeOffset now, int batchSize, TimeSpan lease, CancellationToken cancellationToken);
}

/// <summary>Worker settings (plan: <c>Notifications:*</c>). The retry schedule has a floor of five attempts (DLV-7).</summary>
public sealed class DeliverySettings
{
    public const string SectionName = "Notifications";

    public int ClaimBatchSize { get; set; } = 20;

    public int LeaseSeconds { get; set; } = 120;

    public int PollSeconds { get; set; } = 5;

    public double Jitter { get; set; } = 0.2;

    /// <summary>Minutes to wait after each failed attempt; its length is the number of attempts before a delivery is dead.</summary>
    /// <summary>Where the ops alert for a dead-lettered delivery goes (DLV-3). Empty means log and count only.</summary>
    public string? OpsAlertEmail { get; set; }

    /// <summary>The health check turns degraded when the oldest due delivery has waited longer than this (DLV-11).</summary>
    public int QueueAgeWarnMinutes { get; set; } = 15;

    public int RetentionMonths { get; set; } = 12;

    public int PurgeBatchSize { get; set; } = 500;

    public int PurgeIntervalHours { get; set; } = 24;

    public int[] RetryMinutes { get; set; } = [1, 5, 30, 120, 360, 720];

    public bool IsValid => ClaimBatchSize is >= 1 and <= 200 && RetentionMonths >= 1 && PurgeBatchSize >= 1 && QueueAgeWarnMinutes >= 1 && PurgeIntervalHours >= 1 && LeaseSeconds >= 10 && PollSeconds >= 1 && TryBuildPolicy(out _);

    public RetryPolicy ToRetryPolicy() => new(RetryMinutes.Select(m => TimeSpan.FromMinutes(m)), Jitter);

    private bool TryBuildPolicy(out RetryPolicy? policy)
    {
        try
        {
            policy = ToRetryPolicy();
            return true;
        }
        catch (Romp.BuildingBlocks.Domain.DomainException)
        {
            policy = null;
            return false;
        }
    }
}
