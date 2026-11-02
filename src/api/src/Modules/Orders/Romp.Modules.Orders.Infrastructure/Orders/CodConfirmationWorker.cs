using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.Modules.Orders.Application.Orders;

namespace Romp.Modules.Orders.Infrastructure.Orders;

/// <summary>
/// Runs <see cref="RunCodConfirmationCommand"/> every minute: cancels cash on delivery orders whose final wait passed (COD-2) and raises the
/// WhatsApp request once for opted-in orders (COD-13). The first run waits a minute so it never races the start-up migrations. A failed run is
/// logged and tried again; it never affects a shopper (COD-17, COD-18).
/// </summary>
internal sealed partial class CodConfirmationWorker(IServiceScopeFactory scopes, ILogger<CodConfirmationWorker> logger) : BackgroundService
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
                    var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunCodConfirmationCommand(), stoppingToken);
                    if (result.AutoCancelled > 0 || result.WhatsAppRequested > 0)
                    {
                        LogRan(result.AutoCancelled, result.WhatsAppRequested);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogFailed(ex);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cash on delivery pass: {Cancelled} orders auto-cancelled, {Asked} WhatsApp requests raised.")]
    private partial void LogRan(int cancelled, int asked);

    [LoggerMessage(Level = LogLevel.Error, Message = "The cash on delivery pass failed; it will try again in a minute.")]
    private partial void LogFailed(Exception exception);
}
