using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.Modules.Storefront.Application.ProductRecommendations;

namespace Romp.Modules.Storefront.Infrastructure.ProductRecommendations;

/// <summary>
/// Keeps recommendations fresh (ADR 0029): every <c>Storefront:RecommendationRefreshInterval</c> it copies the recent units sold from Orders and
/// rescores the groups that changed, one group at a time inside a time budget. The first pass runs shortly after start so a new deployment fills
/// the tables. A failing pass is logged and the loop carries on; a page request never waits on this (REC-10).
/// </summary>
internal sealed partial class RecommendationRefreshWorker(IServiceScopeFactory scopes, IOptions<StorefrontOptions> options, ILogger<RecommendationRefreshWorker> logger)
    : BackgroundService
{
    internal static readonly TimeSpan FirstPassDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstPassDelay, stoppingToken);
            using var timer = new PeriodicTimer(options.Value.RecommendationRefreshInterval);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// <summary>One pass: sync the sales, then rescore. Never throws (except for cancellation): a failure is logged.</summary>
    internal async Task<RecommendationPassResult?> RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new SyncProductSalesCommand(), cancellationToken);
            var result = await sender.Send(new RunRecommendationPassCommand(), cancellationToken);
            LogPassDone(result.Groups, result.Products, result.Failed);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogPassFailed(ex);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recommendation pass: {Groups} groups, {Products} products written, {Failed} failed or deferred.")]
    private partial void LogPassDone(int groups, int products, int failed);

    [LoggerMessage(Level = LogLevel.Error, Message = "A recommendation pass failed; it will try again next interval.")]
    private partial void LogPassFailed(Exception exception);
}
