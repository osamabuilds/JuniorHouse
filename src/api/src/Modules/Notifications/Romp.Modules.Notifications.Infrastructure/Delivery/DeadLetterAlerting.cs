using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Email;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Notifications.Application.Delivery;
using Romp.Modules.Notifications.Contracts;
using Romp.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Romp.Modules.Notifications.Infrastructure.Delivery;

/// <summary>The module's metrics. One counter, tagged with kind and channel codes only.</summary>
internal static class NotificationMetrics
{
    public const string MeterName = "Romp.Notifications";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>(
        "romp.notifications.dead_lettered", description: "Deliveries that ran out of attempts.");
}

/// <summary>
/// The alert for a dead-lettered delivery (DLV-3): a log event, a counter and an email to operations. It sends the email itself through
/// the shared port, never through the notification queue, so a broken queue cannot swallow the alert about itself. Behind the inbox, and a
/// failed email makes the outbox retry. The text carries ids and codes only (SEC-25).
/// </summary>
internal sealed class DeadLetterAlertConsumer(
    NtfyDbContext dbContext,
    InboxGuard guard,
    IEmailSender email,
    IOptions<DeliverySettings> settings,
    ILogger<DeadLetterAlertConsumer> logger) : IOutboxMessageHandler<NotificationDeadLetteredEvent>
{
    private static readonly Action<ILogger, long, string, string, Exception?> DeadLettered =
        LoggerMessage.Define<long, string, string>(
            LogLevel.Error, new EventId(10, nameof(DeadLettered)), "Notification {NotificationId} ({Kind}) was dead-lettered on {Channel}.");

    public async Task HandleAsync(NotificationDeadLetteredEvent domainEvent, CancellationToken cancellationToken)
    {
        if (!await guard.TryBeginAsync(dbContext, "AlertOnDeadLetter", cancellationToken))
        {
            return;
        }

        DeadLettered(logger, domainEvent.NotificationId, domainEvent.KindCode, domainEvent.ChannelCode, null);

        var to = settings.Value.OpsAlertEmail;
        if (!string.IsNullOrWhiteSpace(to))
        {
            await email.SendAsync(
                new EmailMessage(
                    to,
                    $"[Romp] Notification {domainEvent.NotificationId} could not be delivered",
                    $"Notification {domainEvent.NotificationId} ({domainEvent.KindCode}) ran out of attempts on {domainEvent.ChannelCode}.\n" +
                    "It is in the dead-letter list and can be resent from the delivery log."),
                cancellationToken);
        }

        NotificationMetrics.DeadLettered.Add(
            1, new KeyValuePair<string, object?>("kind", domainEvent.KindCode), new KeyValuePair<string, object?>("channel", domainEvent.ChannelCode));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Readiness signal for the queue (DLV-11): healthy normally, degraded when the oldest due delivery has waited too long. Never unhealthy.</summary>
internal sealed class QueueAgeHealthCheck(QueueHealth queue, IOptions<DeliverySettings> settings) : IHealthCheck
{
    public const string Name = "notifications-queue";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await queue.ReadAsync(cancellationToken);
            var data = new Dictionary<string, object>
            {
                ["oldestDueSeconds"] = (int)snapshot.OldestDueAge.TotalSeconds,
                ["queued"] = snapshot.QueuedCount,
                ["dead"] = snapshot.DeadCount,
            };

            return snapshot.OldestDueAge > TimeSpan.FromMinutes(settings.Value.QueueAgeWarnMinutes)
                ? HealthCheckResult.Degraded("Notifications are waiting longer than expected.", data: data)
                : HealthCheckResult.Healthy("The notification queue is moving.", data: data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Degraded("The notification queue could not be read.", ex);
        }
    }
}

/// <summary>Runs <see cref="PurgeProcessor"/> once at start and then at the configured interval.</summary>
internal sealed class PurgeWorker(IServiceScopeFactory scopes, IOptions<DeliverySettings> settings, ILogger<PurgeWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> Purged =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(20, nameof(Purged)), "Purged {Count} old notifications.");

    private static readonly Action<ILogger, string, Exception?> PurgeFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(21, nameof(PurgeFailed)), "Notification purge failed: {ExceptionType}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<PurgeProcessor>().RunOnceAsync(stoppingToken);
                if (count > 0)
                {
                    Purged(logger, count, null);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                PurgeFailed(logger, ex.GetType().Name, null);
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(settings.Value.PurgeIntervalHours), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
