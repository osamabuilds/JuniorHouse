using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.PublishedCatalog;

/// <summary>
/// Runs <see cref="ReconcileCommand"/> every <c>Storefront:ReconcileInterval</c> (10 minutes by default; PCT-5). An unexpected error is
/// logged and the loop carries on, so one bad pass never stops the safety net. The first pass waits one interval, so a restart does not
/// pile a full comparison onto start-up.
/// </summary>
internal sealed partial class ReconcileWorker(IServiceScopeFactory scopes, IOptions<StorefrontOptions> options, ILogger<ReconcileWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.ReconcileInterval;
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ReconcileCommand(), stoppingToken);
                    LogPassDone(result.Refreshed, result.Removed, result.LookupCopiesChanged);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogPassFailed(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Storefront reconcile pass: {Refreshed} refreshed, {Removed} removed, {LookupCopies} lookup copies updated.")]
    private partial void LogPassDone(int refreshed, int removed, int lookupCopies);

    [LoggerMessage(Level = LogLevel.Error, Message = "A storefront reconcile pass failed; it will try again next interval.")]
    private partial void LogPassFailed(Exception exception);
}
