using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.Modules.Customers.Application.NotifyMe;

namespace Romp.Modules.Customers.Infrastructure.NotifyMe;

/// <summary>
/// Runs <see cref="PurgeNotifyRequestsCommand"/> once a day. The first run waits a few minutes so it never races the start-up migrations. A
/// failed run is logged and tried again the next day; it never affects a shopper. Nothing logged names an address.
/// </summary>
internal sealed partial class PurgeNotifyRequestsWorker(IServiceScopeFactory scopes, ILogger<PurgeNotifyRequestsWorker> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PurgeNotifyRequestsCommand(), stoppingToken);
                    LogPurged(result.Expired, result.Deleted);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogPurgeFailed(ex);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notify Me expired {Expired} open requests and deleted {Deleted} closed ones.")]
    private partial void LogPurged(int expired, int deleted);

    [LoggerMessage(Level = LogLevel.Error, Message = "Purging Notify Me requests failed; it will try again tomorrow.")]
    private partial void LogPurgeFailed(Exception exception);
}
