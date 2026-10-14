using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Quality.Application.Abstractions;

namespace Romp.Modules.Quality.Application.Receiving;

/// <summary>The paged goods receipt list (AC-42): one COUNT plus one page query however many receipts there are.</summary>
public sealed record SearchGoodsReceiptsQuery(
    string? Search = null,
    short? ReconciliationId = null,
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<GrnSummaryDto>>;

public sealed class SearchGoodsReceiptsQueryHandler(IQualityDbContext dbContext)
    : IRequestHandler<SearchGoodsReceiptsQuery, PagedResult<GrnSummaryDto>>
{
    public async Task<PagedResult<GrnSummaryDto>> Handle(SearchGoodsReceiptsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.GoodsReceipts.AsNoTracking().AsQueryable();
        var batches = dbContext.Batches.AsNoTracking();

        if (request.ReconciliationId is { } reconciliationId)
        {
            query = query.Where(g => g.ReconciliationId == reconciliationId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // GRN and PO numbers are generated in upper case, so an upper-cased needle matches without a locale-sensitive SQL function.
            var text = request.Search.Trim().ToUpperInvariant();
            var raw = request.Search.Trim();
            query = query.Where(g => g.GrnNo.Contains(text) || g.PoNo.Contains(text) || g.VendorDnNo.Contains(raw));
        }

        return await query
            .OrderByDescending(g => g.Id)
            .Select(g => new GrnSummaryDto(
                g.Id, g.GrnNo, g.PoId, g.PoNo, g.VendorDnNo, g.ReceivedDate, g.ReconciliationId, g.ClaimedTotal, g.CountedTotal,
                batches.Where(b => b.GrnId == g.Id).Select(b => (long?)b.Id).FirstOrDefault(),
                batches.Where(b => b.GrnId == g.Id).Select(b => (short?)b.StatusId).FirstOrDefault()))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
    }
}

/// <summary>One goods receipt with its lines and reconciliation (AC-7). Unknown id: not found.</summary>
public sealed record GetGoodsReceiptQuery(long Id) : IRequest<GoodsReceiptDto>;

public sealed class GetGoodsReceiptQueryHandler(IQualityDbContext dbContext)
    : IRequestHandler<GetGoodsReceiptQuery, GoodsReceiptDto>
{
    public async Task<GoodsReceiptDto> Handle(GetGoodsReceiptQuery request, CancellationToken cancellationToken)
    {
        var grn = await dbContext.GoodsReceipts.AsNoTracking().Include(g => g.Lines).FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Goods receipt {request.Id} was not found.");
        return grn.ToDto();
    }
}

/// <summary>
/// A PO's receiving history (AC-9): every goods receipt of the PO with its totals. Bounded by the PO's delivery notes
/// (a handful), so it is a single query rather than a paged list.
/// </summary>
public sealed record ListGoodsReceiptsByPoQuery(long PoId) : IRequest<IReadOnlyList<GrnSummaryDto>>;

public sealed class ListGoodsReceiptsByPoQueryHandler(IQualityDbContext dbContext)
    : IRequestHandler<ListGoodsReceiptsByPoQuery, IReadOnlyList<GrnSummaryDto>>
{
    public async Task<IReadOnlyList<GrnSummaryDto>> Handle(ListGoodsReceiptsByPoQuery request, CancellationToken cancellationToken)
    {
        var batches = dbContext.Batches.AsNoTracking();
        return await dbContext.GoodsReceipts.AsNoTracking()
            .Where(g => g.PoId == request.PoId)
            .OrderBy(g => g.ReceivedDate).ThenBy(g => g.Id)
            .Select(g => new GrnSummaryDto(
                g.Id, g.GrnNo, g.PoId, g.PoNo, g.VendorDnNo, g.ReceivedDate, g.ReconciliationId, g.ClaimedTotal, g.CountedTotal,
                batches.Where(b => b.GrnId == g.Id).Select(b => (long?)b.Id).FirstOrDefault(),
                batches.Where(b => b.GrnId == g.Id).Select(b => (short?)b.StatusId).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}
