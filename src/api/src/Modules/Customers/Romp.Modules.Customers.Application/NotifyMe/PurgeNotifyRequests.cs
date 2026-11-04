using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Customers.Application.Abstractions;
using Romp.Modules.Customers.Domain.NotifyMe;

namespace Romp.Modules.Customers.Application.NotifyMe;

/// <summary>How many requests a purge expired and deleted.</summary>
public sealed record PurgeNotifyRequestsResult(int Expired, int Deleted);

/// <summary>
/// The daily retention job (Q-NTM-2, NFR-26): an open request older than <c>NotifyOpenDays</c> expires, and a closed one (notified or
/// expired) is deleted <c>NotifyClosedDays</c> after it was closed. The addresses are personal data, so they are not kept longer than the
/// purpose needs. Two set-based statements, no row reads. Expiring runs first with the time it was closed set to now, so an expired request
/// always waits the closed period before it is deleted.
/// </summary>
public sealed record PurgeNotifyRequestsCommand : IRequest<PurgeNotifyRequestsResult>, ICustomerCommand;

public sealed class PurgeNotifyRequestsCommandHandler(ICustomerDbContext dbContext, IOptions<CustomersOptions> options, TimeProvider clock)
    : IRequestHandler<PurgeNotifyRequestsCommand, PurgeNotifyRequestsResult>
{
    public async Task<PurgeNotifyRequestsResult> Handle(PurgeNotifyRequestsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var openCutoff = now.AddDays(-options.Value.NotifyOpenDays);
        var closedCutoff = now.AddDays(-options.Value.NotifyClosedDays);
        var open = NotifyStatus.Open;
        var expired = NotifyStatus.Expired;
        var notified = NotifyStatus.Notified;

        // The audit columns are set by hand here: set-based statements bypass the save interceptor.
        var expiredCount = await dbContext.NotifyRequests
            .Where(r => r.Status == open && r.InsrDte < openCutoff)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, expired).SetProperty(r => r.UpdtDte, now).SetProperty(r => r.UpdtBy, "retention"),
                cancellationToken);

        var deleted = await dbContext.NotifyRequests
            .Where(r => (r.Status == notified || r.Status == expired) && (r.UpdtDte ?? r.InsrDte) < closedCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        return new PurgeNotifyRequestsResult(expiredCount, deleted);
    }
}
