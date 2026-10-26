using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Romp.BuildingBlocks.Auditing;
using Romp.BuildingBlocks.Persistence.Inbox;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Identity.Domain.AuditTrail;
using Romp.Modules.Identity.Infrastructure.Persistence;

namespace Romp.Modules.Identity.Infrastructure.AuditTrail;

/// <summary>
/// Stores what every module reports through its outbox (ADR 0016, audit-trail AC-3, AC-5, AC-13). Inbox-guarded: a message delivered
/// twice stores its entries once (and the unique key on the table is the second line of defence). It runs after the business change
/// has committed, so a failure here only delays the entry: the dispatcher retries and, after the limit, dead-letters it where the
/// outbox admin page shows it. The business change is never touched.
/// </summary>
public sealed class RecordAuditHandler(IdntDbContext dbContext, InboxGuard inbox, OutboxMessageContext message)
    : IOutboxMessageHandler<AuditRecorded>
{
    public async Task HandleAsync(AuditRecorded domainEvent, CancellationToken cancellationToken)
    {
        if (!await inbox.TryBeginAsync(dbContext, nameof(RecordAuditHandler), cancellationToken))
        {
            return;
        }

        // A change made before the partition job reached its month would have nowhere to go; creating the month first costs one cheap
        // call and means an entry is never lost to a calendar.
        await AuditPartitions.EnsureAsync(dbContext, domainEvent.OccurredAt, 1, cancellationToken);

        foreach (var record in domainEvent.Records)
        {
            dbContext.AuditEntries.Add(AuditEntry.Create(
                domainEvent.OccurredAt,
                domainEvent.Module,
                message.MessageId,
                record.EntityType,
                record.EntityKey,
                ToOperation(record.Operation),
                domainEvent.Actor,
                domainEvent.CorrelationId,
                record.Columns));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuditOperation ToOperation(string code) => code switch
    {
        AuditRecorded.Insert => AuditOperation.Insert,
        AuditRecorded.Update => AuditOperation.Update,
        AuditRecorded.Delete => AuditOperation.Delete,
        _ => throw new InvalidOperationException("An audit record carried an operation this version does not know."),
    };
}

/// <summary>Creates the monthly partitions of the audit tables (the <c>AUDT_PART_ENSR</c> function from the AddAuditTrail migration).</summary>
internal static class AuditPartitions
{
    public static Task EnsureAsync(IdntDbContext dbContext, DateTimeOffset from, int months, CancellationToken cancellationToken)
    {
        var utc = from.UtcDateTime;
        var firstOfMonth = new DateOnly(utc.Year, utc.Month, 1);
        return dbContext.Database.ExecuteSqlAsync($"""SELECT "IDNT"."AUDT_PART_ENSR"({firstOfMonth}, {months})""", cancellationToken);
    }
}

/// <summary>
/// Keeps the next three months of audit partitions in place, once a day. Handlers also create the month they need, so this is a
/// belt-and-braces job; a failed run is logged and tried again the next day.
/// </summary>
public sealed partial class AuditPartitionWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<AuditPartitionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    public const int MonthsAhead = 4;

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
                    await RunOnceAsync(stoppingToken);
                    LogEnsured(MonthsAhead);
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

    /// <summary>One pass: creates the current month and the next three if they are missing. Idempotent.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        await AuditPartitions.EnsureAsync(dbContext, time.GetUtcNow(), MonthsAhead, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Audit partitions are in place for the next {Months} months.")]
    private partial void LogEnsured(int months);

    [LoggerMessage(Level = LogLevel.Error, Message = "Creating the audit partitions failed; it will try again tomorrow.")]
    private partial void LogFailed(Exception exception);
}
