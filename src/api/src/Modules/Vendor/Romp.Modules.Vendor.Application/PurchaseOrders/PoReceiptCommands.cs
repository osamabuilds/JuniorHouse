using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;
using Romp.Modules.Vendor.Domain.PurchaseOrders;

namespace Romp.Modules.Vendor.Application.PurchaseOrders;

/// <summary>
/// Records that a goods receipt was made against a PO (spec AC-40, AC-41): receipt progress only, never a change of the PO's
/// status (recommended default for Q7; Sprint 3 D2 keeps closing manual). Idempotent by the GRN id, so a redelivered
/// <c>GoodsReceipt.Recorded</c> event is a no-op; a receipt for an unknown PO is ignored.
/// </summary>
public sealed record ApplyGoodsReceiptRecordedCommand(
    long PoId,
    long GrnId,
    string GrnNo,
    DateOnly ReceivedDate,
    IReadOnlyList<ReceiptLineInput> Lines) : IRequest<bool>, IVendorCommand;

public sealed class ApplyGoodsReceiptRecordedCommandHandler(IVendorDbContext dbContext)
    : IRequestHandler<ApplyGoodsReceiptRecordedCommand, bool>
{
    public async Task<bool> Handle(ApplyGoodsReceiptRecordedCommand request, CancellationToken cancellationToken)
    {
        if (await dbContext.PoReceipts.AnyAsync(r => r.GrnId == request.GrnId, cancellationToken)
            || !await dbContext.PurchaseOrders.AnyAsync(p => p.Id == request.PoId, cancellationToken))
        {
            return false;
        }

        dbContext.PoReceipts.Add(new PoReceipt(request.PoId, request.GrnId, request.GrnNo, request.ReceivedDate, request.Lines));
        return true;
    }
}

public sealed record PoReceiptProgressLineDto(short SizeId, short ColourId, int OrderedQty, int ReceivedQty, int OutstandingQty);

public sealed record PoReceiptDto(long GrnId, string GrnNo, DateOnly ReceivedDate, int ClaimedTotal, int CountedTotal);

/// <summary>Received against ordered for one PO (spec AC-40): built from VNDR's own tables only, no internal notes or costs (AC-45).</summary>
public sealed record PoReceiptProgressDto(
    long PoId,
    int OrderedTotal,
    int ReceivedTotal,
    IReadOnlyList<PoReceiptProgressLineDto> Lines,
    IReadOnlyList<PoReceiptDto> Receipts);

/// <summary>Received against ordered per size and colour, and the list of goods receipts. Unknown PO: not found. A constant number of queries.</summary>
public sealed record GetPoReceiptProgressQuery(long PoId) : IRequest<PoReceiptProgressDto>;

public sealed class GetPoReceiptProgressQueryHandler(IVendorDbContext dbContext)
    : IRequestHandler<GetPoReceiptProgressQuery, PoReceiptProgressDto>
{
    public async Task<PoReceiptProgressDto> Handle(GetPoReceiptProgressQuery request, CancellationToken cancellationToken)
    {
        var ordered = await dbContext.PurchaseOrders.AsNoTracking()
            .Where(p => p.Id == request.PoId)
            .Select(p => p.Lines.Select(l => new { l.SizeId, l.ColourId, l.Qty }).ToList())
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        var receipts = await dbContext.PoReceipts.AsNoTracking().Include(r => r.Lines)
            .Where(r => r.PoId == request.PoId)
            .OrderBy(r => r.ReceivedDate).ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        var received = receipts.SelectMany(r => r.Lines)
            .GroupBy(l => (l.SizeId, l.ColourId))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.CountedQty));

        var lines = ordered
            .Select(o => new PoReceiptProgressLineDto(
                o.SizeId, o.ColourId, o.Qty, received.GetValueOrDefault((o.SizeId, o.ColourId)),
                Math.Max(0, o.Qty - received.GetValueOrDefault((o.SizeId, o.ColourId)))))
            .OrderBy(l => l.SizeId).ThenBy(l => l.ColourId)
            .ToList();

        return new PoReceiptProgressDto(
            request.PoId, lines.Sum(l => l.OrderedQty), receipts.Sum(r => r.CountedTotal), lines,
            receipts.Select(r => new PoReceiptDto(r.GrnId, r.GrnNo, r.ReceivedDate, r.ClaimedTotal, r.CountedTotal)).ToList());
    }
}
