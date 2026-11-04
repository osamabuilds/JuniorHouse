using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Application.Delivery;

/// <summary>
/// Deletes notifications older than the retention period (12 months by default) with their deliveries, attempts and dead letters, which the
/// database removes by cascade. A notification that still has queued work is kept. Runs in small batches so it never holds long locks.
/// </summary>
public sealed class PurgeProcessor(INotificationsDbContext dbContext, DeliverySettings settings, TimeProvider clock)
{
    /// <summary>Returns how many notifications were deleted.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddMonths(-settings.RetentionMonths);
        var total = 0;

        while (true)
        {
            var batch = await dbContext.Notifications.AsNoTracking()
                .Where(n => n.InsrDte < cutoff && !n.Deliveries.Any(d => d.Status == DeliveryStatus.Queued))
                .OrderBy(n => n.Id)
                .Select(n => n.Id)
                .Take(settings.PurgeBatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                return total;
            }

            total += await dbContext.Notifications.Where(n => batch.Contains(n.Id)).ExecuteDeleteAsync(cancellationToken);
            if (batch.Count < settings.PurgeBatchSize)
            {
                return total;
            }
        }
    }
}
