using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Production.Application.ProductionRuns;

/// <summary>
/// Opens the production run for a PO when it is acknowledged (spec AC-1..AC-3, BRD 9.10.3
/// PurchaseOrder.Confirmed). Idempotent: an existing run is returned untouched, and a PO that is
/// not acknowledged (Draft, Sent, Cancelled) gets no run. Returns the run id, or null when none was opened.
/// </summary>
public sealed record OpenProductionRunCommand(long PoId) : IRequest<long?>, IProductionCommand;

public sealed class OpenProductionRunCommandHandler(IProductionDbContext dbContext, IPurchaseOrderQueries poQueries)
    : IRequestHandler<OpenProductionRunCommand, long?>
{
    // PO_STS_LKP ids (a system-owned lookup; see Vendor's PoStatus): Acknowledged and every later status except Cancelled.
    private static readonly short[] AcknowledgedOrLater = [3, 5, 6, 7, 8];

    public async Task<long?> Handle(OpenProductionRunCommand request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Runs.AsNoTracking().Where(r => r.PoId == request.PoId).Select(r => (long?)r.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var context = await poQueries.GetProductionContextAsync(request.PoId, cancellationToken);
        if (context is null || !AcknowledgedOrLater.Contains(context.StatusId))
        {
            return null;
        }

        var terms = await poQueries.GetInForceTermsAsync(request.PoId, cancellationToken);
        var run = new ProductionRun(context.PoId, context.PoNo, context.FabricResponsibilityId, context.SentDate, terms?.LatestAcceptableDate);
        dbContext.Runs.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        return run.Id;
    }
}
