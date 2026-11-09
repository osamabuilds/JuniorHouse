using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.Modules.Orders.Application.Payments;

namespace Romp.Modules.Orders.Infrastructure.Payments;

/// <summary>
/// Runs the two payment safety nets (ADR 0027): every minute it expires online orders whose payment window has passed (PAY-11), and every
/// five minutes it asks the gateway about attempts still unanswered after ten, so a charge whose callback was lost is completed exactly once
/// (PAY-6, FR-30). The first run waits a minute so it never races the start-up migrations. A failed run is logged and tried again; it never
/// affects a shopper.
/// </summary>
internal sealed partial class PaymentWorker(IServiceScopeFactory scopes, ILogger<PaymentWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private const int ReconcileEvery = 5;
    private const int PurgeEvery = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Interval, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            var tick = 0;
            do
            {
                tick++;
                await RunAsync(new ExpireUnpaidOrdersCommand(), stoppingToken);
                if (tick % ReconcileEvery == 0)
                {
                    await RunAsync(new ReconcilePaymentsCommand(), stoppingToken);
                }

                if (tick % PurgeEvery == 0)
                {
                    await RunAsync(new PurgePaymentWebhooksCommand(), stoppingToken);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task RunAsync<TResponse>(IRequest<TResponse> request, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(request, stoppingToken);
            LogRan(typeof(TResponse).Name, result);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogFailed(ex, request.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Payment pass {Pass}: {Result}")]
    private partial void LogRan(string pass, object? result);

    [LoggerMessage(Level = LogLevel.Error, Message = "The payment pass {Pass} failed; it will try again.")]
    private partial void LogFailed(Exception exception, string pass);
}
