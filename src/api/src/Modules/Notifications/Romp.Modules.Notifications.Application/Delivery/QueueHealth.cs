using Microsoft.EntityFrameworkCore;
using Romp.Modules.Notifications.Application.Abstractions;
using Romp.Modules.Notifications.Domain.Delivery;

namespace Romp.Modules.Notifications.Application.Delivery;

/// <summary>What the queue looks like right now. <see cref="OldestDueAge"/> is zero when nothing is waiting past its due time (DLV-11).</summary>
public sealed record QueueSnapshot(TimeSpan OldestDueAge, int QueuedCount, int DeadCount);

/// <summary>Reads the queue's age and counts, for the health check and later the delivery-log summary.</summary>
public sealed class QueueHealth(INotificationsDbContext dbContext, TimeProvider clock)
{
    public async Task<QueueSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        var oldestDue = await dbContext.Deliveries.AsNoTracking()
            .Where(d => d.Status == DeliveryStatus.Queued && d.NextAttemptAt <= now)
            .MinAsync(d => d.NextAttemptAt, cancellationToken);

        var counts = await dbContext.Deliveries.AsNoTracking()
            .Where(d => d.Status == DeliveryStatus.Queued || d.Status == DeliveryStatus.Dead)
            .GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new QueueSnapshot(
            oldestDue is { } due && due < now ? now - due : TimeSpan.Zero,
            counts.FirstOrDefault(c => c.Status == DeliveryStatus.Queued)?.Count ?? 0,
            counts.FirstOrDefault(c => c.Status == DeliveryStatus.Dead)?.Count ?? 0);
    }
}
