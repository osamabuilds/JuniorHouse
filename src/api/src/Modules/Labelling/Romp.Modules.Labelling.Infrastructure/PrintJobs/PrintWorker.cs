using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Application.PrintJobs;

namespace Romp.Modules.Labelling.Infrastructure.PrintJobs;

/// <summary>
/// The background worker of ADR 0010: every few seconds it asks the processor to recover stuck jobs and print what is due. An
/// unexpected error is logged and the loop carries on, so a bad job or a dropped connection never stops printing (NFR-SC-02).
/// </summary>
internal sealed partial class PrintWorker(IServiceScopeFactory scopes, IOptions<LabellingOptions> options, ILogger<PrintWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, options.Value.WorkerPollSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PrintJobProcessor>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogPassFailed(ex);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A print worker pass failed; it will try again.")]
    private partial void LogPassFailed(Exception exception);
}
