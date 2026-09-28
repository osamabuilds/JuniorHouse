using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Romp.Modules.Vendor.Application;

/// <summary>AC-14: includes the status timeline, oldest first.</summary>
public sealed record GetPurchaseOrderByIdQuery(long Id) : IRequest<PoDto?>;

public sealed class GetPurchaseOrderByIdQueryHandler(IVendorDbContext dbContext)
    : IRequestHandler<GetPurchaseOrderByIdQuery, PoDto?>
{
    public async Task<PoDto?> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var po = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Lines)
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        return po?.ToDto();
    }
}
