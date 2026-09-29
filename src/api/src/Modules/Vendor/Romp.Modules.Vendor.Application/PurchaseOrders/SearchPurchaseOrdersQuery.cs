using MediatR;
using Romp.BuildingBlocks.Application.Paging;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Vendor.Application.Abstractions;

namespace Romp.Modules.Vendor.Application.PurchaseOrders;

/// <summary>Search/list for the Purchase Orders admin screen - filter by vendor/status/date.</summary>
public sealed record SearchPurchaseOrdersQuery(
    long? VendorId = null,
    short? StatusId = null,
    DateOnly? ExpectedDeliveryFrom = null,
    DateOnly? ExpectedDeliveryTo = null,
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<PoSummaryDto>>;

public sealed class SearchPurchaseOrdersQueryHandler(IVendorDbContext dbContext)
    : IRequestHandler<SearchPurchaseOrdersQuery, PagedResult<PoSummaryDto>>
{
    public async Task<PagedResult<PoSummaryDto>> Handle(SearchPurchaseOrdersQuery request, CancellationToken cancellationToken)
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

        // Projected in SQL (no lines/history loaded) and ordered by a unique key so pages are stable.
        return await query
            .OrderByDescending(p => p.ExpectedDeliveryDate).ThenByDescending(p => p.Id)
            .Select(p => new PoSummaryDto(p.Id, p.PoNo, p.VendorId, p.StatusId, p.ExpectedDeliveryDate))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
    }
}
