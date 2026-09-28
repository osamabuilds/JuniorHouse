using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>The VNDR side of the CTLG-facing contract (SCRUM-93, AC-2/AC-3).</summary>
public sealed class PurchaseOrderUsageQueries(VendorDbContext dbContext) : IPurchaseOrderUsageQueries
{
    public async Task<IReadOnlyCollection<PoSizeColourUsage>> GetActiveSizeColourUsageAsync(
        long styleId, CancellationToken cancellationToken)
    {
        var currentLineUsage = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Lines)
            .Where(po => po.StyleId == styleId && po.StatusId != 4) // PoStatus.Cancelled - internal to Vendor.Domain, not visible across assemblies
            .SelectMany(po => po.Lines, (po, line) => new PoSizeColourUsage(line.SizeId, line.ColourId, po.PoNo))
            .ToListAsync(cancellationToken);

        // SCRUM-93 task 28: PO_LINE only mirrors the In-force revision, so an open Pending
        // revision's own proposed lines need a separate query (AC-2's own wording covers both).
        var pendingRevisionLineUsage = await dbContext.Set<Domain.PurchaseOrderRevision>()
            .AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.StatusId == 1) // RevisionStatus.Pending - internal to Vendor.Domain
            .Join(
                dbContext.PurchaseOrders.Where(po => po.StyleId == styleId && po.StatusId != 4),
                revision => revision.PoId,
                po => po.Id,
                (revision, po) => new { revision, po.PoNo })
            .SelectMany(x => x.revision.Lines, (x, line) => new PoSizeColourUsage(line.SizeId, line.ColourId, x.PoNo))
            .ToListAsync(cancellationToken);

        return currentLineUsage.Concat(pendingRevisionLineUsage).Distinct().ToList();
    }
}
