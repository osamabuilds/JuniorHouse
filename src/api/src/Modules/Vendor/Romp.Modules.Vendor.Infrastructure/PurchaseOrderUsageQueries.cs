using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Contracts;

namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>The VNDR side of the CTLG-facing contract (SCRUM-93, AC-2/AC-3).</summary>
public sealed class PurchaseOrderUsageQueries(VendorDbContext dbContext) : IPurchaseOrderUsageQueries
{
    public async Task<IReadOnlyCollection<PoSizeColourUsage>> GetActiveSizeColourUsageAsync(
        long styleId, CancellationToken cancellationToken) =>
        await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Lines)
            .Where(po => po.StyleId == styleId && po.StatusId != 4) // PoStatus.Cancelled - internal to Vendor.Domain, not visible across assemblies
            .SelectMany(po => po.Lines, (po, line) => new PoSizeColourUsage(line.SizeId, line.ColourId, po.PoNo))
            .ToListAsync(cancellationToken);
}
