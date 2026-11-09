using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.Modules.Cart.Application.DiscountCodes;

namespace Romp.Modules.Cart.Infrastructure.DiscountCodes;

/// <summary>
/// Runs <see cref="SweepHeldRedemptionsCommand"/> every minute. The first run waits a minute so it never races the start-up migrations. A failed
/// run is logged and tried again a minute later; it never affects a shopper (ADR 0025).
/// </summary>
internal sealed partial class DiscountHoldSweeper(IServiceScopeFactory scopes, ILogger<DiscountHoldSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

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
                    var released = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SweepHeldRedemptionsCommand(), stoppingToken);
                    if (released > 0)
                    {
                        LogSwept(released);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogSweepFailed(ex);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cart released {Count} held discount redemptions that never got an order.")]
    private partial void LogSwept(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Releasing stale discount holds failed; it will try again in a minute.")]
    private partial void LogSweepFailed(Exception exception);
}
