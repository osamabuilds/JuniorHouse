using System.Threading.Channels;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.Modules.Storefront.Application.Search;
using Romp.Modules.Storefront.Infrastructure.Persistence;

namespace Romp.Modules.Storefront.Infrastructure.Search;

/// <summary>Counts zero-result searches with one statement, so two visitors missing at once cannot lose a count (search plan, Concurrency).</summary>
public sealed class SearchMissStore(StfrDbContext db) : ISearchMissStore
{
    private const string Actor = "storefront";

    public async Task RecordAsync(string text, DateOnly day, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "STFR"."SRCH_MISS" ("QRY_TEXT", "MISS_DT", "MISS_CNT", "INSR_DTE", "INSR_BY")
            VALUES ({text}, {day}, 1, now(), {Actor})
            ON CONFLICT ("QRY_TEXT", "MISS_DT")
            DO UPDATE SET "MISS_CNT" = "SRCH_MISS"."MISS_CNT" + 1, "UPDT_DTE" = now(), "UPDT_BY" = {Actor}
            """, cancellationToken);

    public async Task<int> PurgeAsync(DateOnly before, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlAsync($"""DELETE FROM "STFR"."SRCH_MISS" WHERE "MISS_DT" < {before}""", cancellationToken);
}

/// <summary>
/// Records zero-result searches after the response and purges old counts once a day. The queue is bounded: when it is full the text is
/// dropped. A failed write is logged without the text and never reaches the visitor.
/// </summary>
internal sealed partial class SearchMissWorker(IServiceScopeFactory scopes, ILogger<SearchMissWorker> logger) : BackgroundService, ISearchMissSink
{
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromDays(1);

    private readonly Channel<string> _queue = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
    });

    public void Offer(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            _queue.Writer.TryWrite(text);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(RecordLoopAsync(stoppingToken), PurgeLoopAsync(stoppingToken));

    private async Task RecordLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var text in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecordMissCommand(text), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // The text is data from a visitor: it is not logged.
                    LogRecordFailed(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task PurgeLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PurgeInterval);
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var removed = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new PurgeMissesCommand(), stoppingToken);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "A zero-result search could not be recorded.")]
    private partial void LogRecordFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Storefront purged {Removed} old zero-result search counts.")]
    private partial void LogPurged(int removed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Purging old zero-result search counts failed; it will try again tomorrow.")]
    private partial void LogPurgeFailed(Exception exception);
}
