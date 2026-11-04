using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.Modules.Identity.Application.Sessions;

namespace Romp.Modules.Identity.Infrastructure.Sessions;

/// <summary>
/// Runs <see cref="PurgeExpiredSessionsCommand"/> once a day. The first run waits a few minutes so it never races the start-up
/// migrations. A failed run is logged and tried again the next day; it never affects sign-in.
/// </summary>
internal sealed partial class SessionPurgeWorker(IServiceScopeFactory scopes, ILogger<SessionPurgeWorker> logger) : BackgroundService
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
                    var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PurgeExpiredSessionsCommand(), stoppingToken);
                    LogPurged(result.Sessions, result.Attempts, result.PendingSignIns);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Identity purged {Sessions} old sessions, {Attempts} sign-in attempts and {Pending} unfinished sign-ins.")]
    private partial void LogPurged(int sessions, int attempts, int pending);

    [LoggerMessage(Level = LogLevel.Error, Message = "Purging old sign-in history failed; it will try again tomorrow.")]
    private partial void LogPurgeFailed(Exception exception);
}
