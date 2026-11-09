using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.Modules.Fulfilment.Application.Abstractions;

namespace Romp.Modules.Fulfilment.Application.PickLists;

/// <summary>
/// Deletes scan-log rows older than <c>Fulfilment:ScanLogRetentionDays</c> (the log is compact, not audited, and high volume; ADR 0030). One
/// statement, indexed on the scan time. Returns how many rows went.
/// </summary>
public sealed record PurgeScanLogCommand : IRequest<int>, IFulfilmentCommand;

public sealed class PurgeScanLogCommandHandler(IFulfilmentDbContext dbContext, IOptions<FulfilmentOptions> options, TimeProvider clock) : IRequestHandler<PurgeScanLogCommand, int>
{
    public async Task<int> Handle(PurgeScanLogCommand request, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.ScanLogRetentionDays);
        return await dbContext.Scans.Where(s => s.At < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}
