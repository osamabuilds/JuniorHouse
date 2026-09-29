using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Application.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Files;

namespace Romp.Modules.Vendor.Application.VendorView;

/// <summary>SCRUM-93 task 42 (AC-35). Returns null for a PO that was never sent - a Draft isn't something a vendor may see.</summary>
public sealed record GetVendorFacingPoViewQuery(long PoId) : IRequest<VendorPoViewDto?>;

public sealed class GetVendorFacingPoViewQueryHandler(IVendorDbContext dbContext)
    : IRequestHandler<GetVendorFacingPoViewQuery, VendorPoViewDto?>
{
    private const short DraftStatusId = 1;
    private const short PendingStatusId = 1;
    private const short InForceStatusId = 2;

    public async Task<VendorPoViewDto?> Handle(GetVendorFacingPoViewQuery request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders.AsNoTracking()
            .Include(p => p.Lines)
            .Include(p => p.Revisions).ThenInclude(r => r.Lines)
            .FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken);

        if (po is null || po.StatusId == DraftStatusId)
        {
            return null;
        }

        var vendorName = await dbContext.Vendors.AsNoTracking()
            .Where(v => v.Id == po.VendorId).Select(v => v.Name).FirstAsync(cancellationToken);

        var inForce = po.Revisions.FirstOrDefault(r => r.StatusId == InForceStatusId);
        var revisionNumber = inForce?.RevisionNumber ?? (short)0;

        var files = await dbContext.PurchaseOrderFiles.AsNoTracking()
            .Where(f => f.PoId == po.Id && !f.IsDeleted).ToListAsync(cancellationToken);
        var effectiveFiles = PoFileEffectiveSet.At(files, po.Revisions, revisionNumber)
            .Select(f => new VendorPoFileDto(f.Id, f.FileName, f.CategoryId))
            .ToList();

        var pending = po.Revisions.FirstOrDefault(r => r.StatusId == PendingStatusId);

        return new VendorPoViewDto(
            po.PoNo, vendorName, po.StyleId, po.StatusId, revisionNumber,
            po.UnitCost, po.ExpectedDeliveryDate, po.LatestAcceptableDate, po.OverTolerancePercent, po.UnderTolerancePercent,
            po.PaymentTermId, po.AdvancePercent, po.FabricResponsibilityId,
            po.Lines.Select(l => new PoLineDto(l.SizeId, l.ColourId, l.Qty)).ToList(),
            effectiveFiles,
            pending is null
                ? null
                : new VendorPendingRevisionDto(
                    pending.RevisionNumber, pending.InitiatorId, pending.VendorMessage,
                    pending.UnitCost, pending.ExpectedDeliveryDate, pending.LatestAcceptableDate));
    }
}
