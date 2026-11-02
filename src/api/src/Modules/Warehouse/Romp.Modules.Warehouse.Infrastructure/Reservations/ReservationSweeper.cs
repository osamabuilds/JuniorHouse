using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.Modules.Warehouse.Application.Reservations;

namespace Romp.Modules.Warehouse.Infrastructure.Reservations;

/// <summary>
/// Runs <see cref="SweepHeldReservationsCommand"/> every minute, so a hold that no order confirmed cannot keep stock for ever (RSV-6). The
/// first run waits a minute so it never races the start-up migrations. A failed run is logged and tried again a minute later; it never
/// affects a shopper. A late order-placed event that finds its hold already released is handled by the confirm handler, not here.
/// </summary>
internal sealed partial class ReservationSweeper(IServiceScopeFactory scopes, ILogger<ReservationSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Interval, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var released = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SweepHeldReservationsCommand(), stoppingToken);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Warehouse released {Count} stock reservations that no order confirmed in time.")]
    private partial void LogSwept(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Releasing expired stock reservations failed; it will try again in a minute.")]
    private partial void LogSweepFailed(Exception exception);
}
