using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.Modules.Storefront.Application.PublishedCatalog;

namespace Romp.Modules.Storefront.Infrastructure.PublishedCatalog;

/// <summary>
/// Drains the derivative queue (PCT-7): sends <see cref="ProcessDerivativeCommand"/> until nothing is due, then sleeps
/// <c>Storefront:ImageWorkerPollInterval</c>. Each derivative is its own transaction, so a crash loses at most the one in hand and its row
/// lock is released with the connection. An unexpected error is logged and the loop carries on after the same pause.
/// </summary>
internal sealed partial class DerivativeWorker(IServiceScopeFactory scopes, IOptions<StorefrontOptions> options, ILogger<DerivativeWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var idle = true;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var outcome = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessDerivativeCommand(), stoppingToken);
                idle = outcome == DerivativeOutcome.NothingDue;
                if (outcome == DerivativeOutcome.Failed)
                {
                    LogDerivativeFailed();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogPassFailed(ex);
            }

            if (idle)
            {
                try
                {
                    await Task.Delay(options.Value.ImageWorkerPollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A storefront image derivative was marked failed; see ERR_CODE on STFR.IMG_DRV.")]
    private partial void LogDerivativeFailed();

    [LoggerMessage(Level = LogLevel.Error, Message = "The storefront image worker hit an unexpected error; it will try again shortly.")]
    private partial void LogPassFailed(Exception exception);
}
