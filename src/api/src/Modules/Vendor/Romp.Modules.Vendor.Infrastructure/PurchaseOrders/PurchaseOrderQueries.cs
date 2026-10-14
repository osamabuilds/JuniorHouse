using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Vendor.Domain.Files;
using Romp.Modules.Vendor.Domain.Revisions;
using Romp.Modules.Vendor.Infrastructure.Persistence;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

/// <summary>The VNDR side of <see cref="IPurchaseOrderQueries"/> (SCRUM-93 task 46, AC-51).</summary>
public sealed class PurchaseOrderQueries(VendorDbContext dbContext) : IPurchaseOrderQueries
{
    private const short DraftStatusId = 1;
    private const short InForceRevisionStatusId = 2;

    public async Task<PoInForceTerms?> GetInForceTermsAsync(long poId, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders.AsNoTracking().AsSplitQuery()
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

    /// <summary>Pakistan has no daylight saving; a fixed offset avoids depending on tzdata in a slim container.</summary>
    private static readonly TimeSpan PakistanOffset = TimeSpan.FromHours(5);

    private const short SentToVendorStatusId = 2;

    private static DateOnly PakistanDate(DateTimeOffset instant) => DateOnly.FromDateTime(instant.ToOffset(PakistanOffset).DateTime);

    public async Task<PoProductionContext?> GetProductionContextAsync(long poId, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders.AsNoTracking()
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == poId, cancellationToken);

        var sent = po?.StatusHistory.Where(h => h.PoStatusId == SentToVendorStatusId).OrderBy(h => h.InsrDte).FirstOrDefault();
        return po is null || sent is null
            ? null
            : new PoProductionContext(po.Id, po.PoNo, po.StatusId, PakistanDate(sent.InsrDte), po.FabricResponsibilityId);
    }

    public async Task<PoInForceTerms?> GetTermsAtAsync(long poId, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders.AsNoTracking().AsSplitQuery()
            .Include(p => p.Revisions).ThenInclude(r => r.Lines)
            .Include(p => p.Revisions).ThenInclude(r => r.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == poId, cancellationToken);

        if (po is null || po.StatusId == DraftStatusId || po.Revisions.Count == 0)
        {
            return null;
        }

        // In force on a day means in force at the end of that Pakistan-time day.
        var cutoff = new DateTimeOffset(asOfDate.AddDays(1).ToDateTime(TimeOnly.MinValue), PakistanOffset);

        var candidates = po.Revisions
            .Select(r => (Revision: r, InForceAt: r.StatusHistory.Where(h => h.ToStatusId == InForceRevisionStatusId).Select(h => (DateTimeOffset?)h.InsrDte).Min()))
            .Where(x => x.InForceAt is not null)
            .ToList();

        // A date before the PO was sent, or backfilled data with no put-in-force time, falls back to the earliest revision (Rev 0 counts from Send).
        var chosen = candidates.Where(x => x.InForceAt < cutoff).OrderByDescending(x => x.InForceAt).ThenByDescending(x => x.Revision.RevisionNumber)
            .Select(x => x.Revision).FirstOrDefault()
            ?? po.Revisions.OrderBy(r => r.RevisionNumber).First();

        return new PoInForceTerms(
            po.Id, po.PoNo, po.VendorId, po.StyleId, po.StatusId, chosen.RevisionNumber,
            chosen.UnitCost, chosen.ExpectedDeliveryDate, chosen.LatestAcceptableDate, chosen.OverTolerancePercent, chosen.UnderTolerancePercent,
            chosen.PaymentTermId, chosen.AdvancePercent, chosen.FabricResponsibilityId,
            chosen.Lines.Select(l => new PoTermsLineContract(l.SizeId, l.ColourId, l.Qty)).ToList());
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
