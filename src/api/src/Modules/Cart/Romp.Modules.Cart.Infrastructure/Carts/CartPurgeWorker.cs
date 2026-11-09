using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.Modules.Cart.Application.Carts;

namespace Romp.Modules.Cart.Infrastructure.Carts;

/// <summary>
/// Runs <see cref="PurgeExpiredCartsCommand"/> once a day. The first run waits a few minutes so it never races the start-up migrations. A failed
/// run is logged and tried again the next day; it never affects a shopper (CRT-10).
/// </summary>
internal sealed partial class CartPurgeWorker(IServiceScopeFactory scopes, ILogger<CartPurgeWorker> logger) : BackgroundService
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
                    var removed = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PurgeExpiredCartsCommand(), stoppingToken);
                    LogPurged(removed);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Cart purged {Count} expired guest baskets.")]
    private partial void LogPurged(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Purging expired guest baskets failed; it will try again tomorrow.")]
    private partial void LogPurgeFailed(Exception exception);
}
