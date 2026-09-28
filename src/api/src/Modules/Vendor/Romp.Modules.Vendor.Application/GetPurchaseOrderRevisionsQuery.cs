using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Romp.Modules.Vendor.Application;

/// <summary>
/// SCRUM-93 task 27 (AC-34). Full revision history, oldest first, each already carrying its own
/// before/after/diff impact figures against whatever was In-force when it was created (task 20/22).
/// Communication details and evidence aren't returned yet - vendor communication capture (task 30+)
/// and spec files (task 34+) don't exist as concepts in this query's data source yet.
/// </summary>
public sealed record GetPurchaseOrderRevisionsQuery(long PoId) : IRequest<IReadOnlyCollection<PoRevisionDto>>;

public sealed class GetPurchaseOrderRevisionsQueryHandler(IVendorDbContext dbContext)
    : IRequestHandler<GetPurchaseOrderRevisionsQuery, IReadOnlyCollection<PoRevisionDto>>
{
    public async Task<IReadOnlyCollection<PoRevisionDto>> Handle(GetPurchaseOrderRevisionsQuery request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Revisions).ThenInclude(r => r.Lines)
            .FirstOrDefaultAsync(p => p.Id == request.PoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Purchase order {request.PoId} was not found.");

        return po.Revisions
            .OrderBy(r => r.RevisionNumber)
            .Select(r => r.ToDto())
            .ToList();
    }
}
