using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Romp.BuildingBlocks.Persistence.Outbox;

/// <summary>The generic hosted service that drives <see cref="IOutboxDispatcher"/> on a fixed poll interval (SCRUM-181). One instance for every registered module - never one per module (plan.md's rejected-alternative note).</summary>
public sealed partial class OutboxDispatcherHostedService(
    IOutboxDispatcher dispatcher,
    OutboxDispatcherOptions options,
    ILogger<OutboxDispatcherHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await dispatcher.DispatchOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPassFailed(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox dispatcher pass failed; will retry next poll interval.")]
    private partial void LogPassFailed(Exception exception);
}
