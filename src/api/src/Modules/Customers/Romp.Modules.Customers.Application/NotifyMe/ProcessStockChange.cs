using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.NotifyMe;

namespace Romp.Modules.Customers.Application.NotifyMe;

/// <summary>
/// A variant's stock changed (FR-11, NTM-4). When units are available, every open request for the variant is closed and one restock
/// email is queued for each through the outbox, in this one transaction, so a request is notified exactly once and the email can be
/// retried on its own without touching stock or the request (NTM-8, NFR-FT-08). Returns how many requests were notified.
/// </summary>
public sealed record ProcessStockChangeCommand(long VariantId, int Available) : IRequest<int>, ICustomerCommand;

public sealed class ProcessStockChangeCommandHandler(ICustomerDbContext dbContext, TimeProvider clock) : IRequestHandler<ProcessStockChangeCommand, int>
{
    /// <summary>How many requests are loaded and closed at a time, so a popular variant is never one huge read.</summary>
    public const int BatchSize = 500;

    public async Task<int> Handle(ProcessStockChangeCommand request, CancellationToken cancellationToken)
    {
        if (!RestockRule.IsRestock(request.Available))
        {
            return 0;
        }

        var now = clock.GetUtcNow();
        var open = NotifyStatus.Open;
        var total = 0;
        while (true)
        {
            // Closed rows no longer match, so the next pass reads the next batch. A request that expires or is closed meanwhile makes the
            // save fail on its row version, and the whole message is retried (the inbox row is not saved either).
            var batch = await dbContext.NotifyRequests
                .Where(r => r.VariantId == request.VariantId && r.Status == open)
                .OrderBy(r => r.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                return total;
            }

            foreach (var notify in batch)
            {
                if (notify.MarkNotified(now))
                {
                    notify.QueueRestockNotice();
                    total++;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
