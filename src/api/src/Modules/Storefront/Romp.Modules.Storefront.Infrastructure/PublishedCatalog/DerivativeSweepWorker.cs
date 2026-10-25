using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.StorefrontSettings;

namespace Romp.Modules.Storefront.Infrastructure.PublishedCatalog;

/// <summary>Runs <see cref="SweepDerivativesCommand"/> every <c>Storefront:DerivativeSweepInterval</c> (daily; PCT-10). A failed pass is logged and retried next interval.</summary>
internal sealed partial class DerivativeSweepWorker(IServiceScopeFactory scopes, IOptions<StorefrontOptions> options, ILogger<DerivativeSweepWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.DerivativeSweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var removed = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SweepDerivativesCommand(), stoppingToken);
                    LogSwept(removed);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogFailed(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Storefront derivative sweep removed {Removed} unreferenced files.")]
    private partial void LogSwept(int removed);

    [LoggerMessage(Level = LogLevel.Error, Message = "A storefront derivative sweep failed; it will try again next interval.")]
    private partial void LogFailed(Exception exception);
}
