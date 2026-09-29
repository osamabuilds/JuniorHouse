using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Quality.Application.Abstractions;

namespace Romp.Modules.Quality.Application.InboundDeliveries;

/// <summary>One row of the receiving queue. <c>GrnId</c> is null while nothing has been received (the "open" deliveries).</summary>
public sealed record InboundDeliverySummaryDto(
    long Id,
    long RunId,
    long PoId,
    string PoNo,
    string VendorDnNo,
    DateOnly DispatchDate,
    bool IsFinalShipment,
    bool IsCancelled,
    int ClaimedTotal,
    long? GrnId,
    string? GrnNo);

/// <summary>
/// The paged receiving queue (AC-42, CONVENTIONS section 4): one COUNT plus one page query however many deliveries there
/// are. <c>Status</c> is <c>open</c> (no goods receipt yet, not cancelled), <c>received</c>, <c>cancelled</c> or empty for all.
/// </summary>
public sealed record SearchInboundDeliveriesQuery(
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<InboundDeliverySummaryDto>>;

public sealed class SearchInboundDeliveriesQueryHandler(IQualityDbContext dbContext)
    : IRequestHandler<SearchInboundDeliveriesQuery, PagedResult<InboundDeliverySummaryDto>>
{
    public async Task<PagedResult<InboundDeliverySummaryDto>> Handle(SearchInboundDeliveriesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Deliveries.AsNoTracking().AsQueryable();
        var receipts = dbContext.GoodsReceipts.AsNoTracking();

        switch (request.Status?.Trim().ToLowerInvariant())
        {
            case "open":
                query = query.Where(d => !d.IsCancelled && !receipts.Any(g => g.DeliveryId == d.Id));
                break;
            case "received":
                query = query.Where(d => receipts.Any(g => g.DeliveryId == d.Id));
                break;
            case "cancelled":
                query = query.Where(d => d.IsCancelled);
                break;
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // PO numbers are generated in upper case, so an upper-cased needle matches without a locale-sensitive SQL function.
            var text = request.Search.Trim().ToUpperInvariant();
            var raw = request.Search.Trim();
            query = query.Where(d => d.PoNo.Contains(text) || d.VendorDnNo.Contains(raw));
        }

        // Everything is computed in SQL from QC's own rows; no entity is loaded and nothing runs per row.
        return await query
            .OrderByDescending(d => d.Id)
            .Select(d => new InboundDeliverySummaryDto(
                d.Id, d.RunId, d.PoId, d.PoNo, d.VendorDnNo, d.DispatchDate, d.IsFinalShipment, d.IsCancelled,
                d.Lines.Sum(l => l.ClaimedQty),
                receipts.Where(g => g.DeliveryId == d.Id).Select(g => (long?)g.Id).FirstOrDefault(),
                receipts.Where(g => g.DeliveryId == d.Id).Select(g => g.GrnNo).FirstOrDefault()))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
    }
}
