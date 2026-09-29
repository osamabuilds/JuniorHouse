using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Vendor.Domain;
using Romp.Modules.Vendor.Domain.Files;
using Romp.Modules.Vendor.Domain.Revisions;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure.Persistence;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

/// <summary>The VNDR side of <see cref="IPurchaseOrderQueries"/> (SCRUM-93 task 46, AC-51).</summary>
public sealed class PurchaseOrderQueries(VendorDbContext dbContext) : IPurchaseOrderQueries
{
    private const short DraftStatusId = 1;
    private const short InForceRevisionStatusId = 2;

    public async Task<PoInForceTerms?> GetInForceTermsAsync(long poId, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders.AsNoTracking()
            .Include(p => p.Lines)
            .Include(p => p.Revisions)
            .FirstOrDefaultAsync(p => p.Id == poId, cancellationToken);

        if (po is null || po.StatusId == DraftStatusId)
        {
            return null;
        }

        // The PO row mirrors the in-force revision (ADR 0007), so the terms come from it directly.
        var revisionNumber = po.Revisions.FirstOrDefault(r => r.StatusId == InForceRevisionStatusId)?.RevisionNumber ?? (short)0;

        return new PoInForceTerms(
            po.Id, po.PoNo, po.VendorId, po.StyleId, po.StatusId, revisionNumber,
            po.UnitCost, po.ExpectedDeliveryDate, po.LatestAcceptableDate, po.OverTolerancePercent, po.UnderTolerancePercent,
            po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId,
            po.Lines.Select(l => new PoTermsLineContract(l.SizeId, l.ColourId, l.Qty)).ToList());
    }

    public async Task<IReadOnlyList<PoRevisionSummary>> ListRevisionsAsync(long poId, CancellationToken cancellationToken) =>
        await dbContext.Set<PurchaseOrderRevision>().AsNoTracking()
            .Where(r => r.PoId == poId)
            .OrderBy(r => r.RevisionNumber)
            .Select(r => new PoRevisionSummary(
                r.RevisionNumber, r.StatusId, r.InitiatorId, r.ReasonId,
                r.UnitCost, r.ExpectedDeliveryDate, r.LatestAcceptableDate, r.InsrDte))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PoEffectiveFile>> GetEffectiveFilesAsync(
        long poId, short? revisionNumber, CancellationToken cancellationToken)
    {
        var revisions = await dbContext.Set<PurchaseOrderRevision>().AsNoTracking()
            .Where(r => r.PoId == poId).ToListAsync(cancellationToken);
        var files = await dbContext.Set<PurchaseOrderFile>().AsNoTracking()
            .Where(f => f.PoId == poId && !f.IsDeleted).ToListAsync(cancellationToken);

        var target = revisionNumber
            ?? revisions.FirstOrDefault(r => r.StatusId == InForceRevisionStatusId)?.RevisionNumber
            ?? 0;

        return PoFileEffectiveSet.At(files, revisions, target)
            .Select(f => new PoEffectiveFile(f.Id, f.FileName, f.CategoryId))
            .ToList();
    }
}
