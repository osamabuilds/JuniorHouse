using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Romp.Modules.Vendor.Application;

/// <summary>Search/list for the Purchase Orders admin screen - filter by vendor/status/date.</summary>
public sealed record SearchPurchaseOrdersQuery(
    long? VendorId = null,
    short? StatusId = null,
    DateOnly? ExpectedDeliveryFrom = null,
    DateOnly? ExpectedDeliveryTo = null) : IRequest<IReadOnlyList<PoSummaryDto>>;

public sealed class SearchPurchaseOrdersQueryHandler(IVendorDbContext dbContext)
    : IRequestHandler<SearchPurchaseOrdersQuery, IReadOnlyList<PoSummaryDto>>
{
    public async Task<IReadOnlyList<PoSummaryDto>> Handle(SearchPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.PurchaseOrders.AsNoTracking().AsQueryable();

        if (request.VendorId is { } vendorId)
        {
            query = query.Where(p => p.VendorId == vendorId);
        }

        if (request.StatusId is { } statusId)
        {
            query = query.Where(p => p.StatusId == statusId);
        }

        if (request.ExpectedDeliveryFrom is { } from)
        {
            query = query.Where(p => p.ExpectedDeliveryDate >= from);
        }

        if (request.ExpectedDeliveryTo is { } to)
        {
            query = query.Where(p => p.ExpectedDeliveryDate <= to);
        }

        var orders = await query.OrderByDescending(p => p.ExpectedDeliveryDate).ToListAsync(cancellationToken);

        return orders.Select(p => p.ToSummaryDto()).ToList();
    }
}
