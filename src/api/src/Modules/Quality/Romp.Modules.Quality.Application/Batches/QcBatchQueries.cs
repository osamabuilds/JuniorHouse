using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Application.Inspections.Aql;
using Romp.Modules.Quality.Domain.Inspections.Aql;

namespace Romp.Modules.Quality.Application.Batches;

/// <summary>The paged QC batch list (AC-42): one COUNT plus one page query however many batches there are.</summary>
public sealed record SearchQcBatchesQuery(
    short? StatusId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<QcBatchSummaryDto>>;

public sealed class SearchQcBatchesQueryHandler(IQualityDbContext dbContext)
    : IRequestHandler<SearchQcBatchesQuery, PagedResult<QcBatchSummaryDto>>
{
    public async Task<PagedResult<QcBatchSummaryDto>> Handle(SearchQcBatchesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Batches.AsNoTracking().AsQueryable();
        var receipts = dbContext.GoodsReceipts.AsNoTracking();

        if (request.StatusId is { } statusId)
        {
            query = query.Where(b => b.StatusId == statusId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var text = request.Search.Trim().ToUpperInvariant();
            var raw = request.Search.Trim();
            query = query.Where(b => b.PoNo.Contains(text) || receipts.Any(g => g.Id == b.GrnId && (g.GrnNo.Contains(text) || g.VendorDnNo.Contains(raw))));
        }

        return await query
            .OrderByDescending(b => b.Id)
            .Select(b => new QcBatchSummaryDto(
                b.Id, b.GrnId,
                receipts.Where(g => g.Id == b.GrnId).Select(g => g.GrnNo).FirstOrDefault()!,
                b.PoId, b.PoNo,
                receipts.Where(g => g.Id == b.GrnId).Select(g => g.VendorDnNo).FirstOrDefault()!,
                b.StatusId, b.LotQty,
                b.Inspections.OrderByDescending(i => i.Id).Select(i => (short?)i.ResultId).FirstOrDefault()))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
    }
}

/// <summary>One batch with its lines, inspections (checklist and defects) and sub-batches. Unknown id: not found.</summary>
public sealed record GetQcBatchQuery(long Id) : IRequest<QcBatchDto>;

public sealed class GetQcBatchQueryHandler(IQualityDbContext dbContext) : IRequestHandler<GetQcBatchQuery, QcBatchDto>
{
    public async Task<QcBatchDto> Handle(GetQcBatchQuery request, CancellationToken cancellationToken)
    {
        // One query loads the batch graph; the receipt's number comes from one more, not one per row.
        var batch = await dbContext.Batches.AsNoTracking().AsSplitQuery()
            .Include(b => b.Lines)
            .Include(b => b.SubBatches).ThenInclude(s => s.Lines)
            .Include(b => b.SubBatches).ThenInclude(s => s.Returns)
            .Include(b => b.Inspections).ThenInclude(i => i.Checks)
            .Include(b => b.Inspections).ThenInclude(i => i.Defects)
            .Include(b => b.Inspections).ThenInclude(i => i.SafetyChecks).ThenInclude(s => s.AffectedLines)
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"QC batch {request.Id} was not found.");
        var receipt = await dbContext.GoodsReceipts.AsNoTracking()
            .Where(g => g.Id == batch.GrnId).Select(g => new { g.GrnNo, g.VendorDnNo }).SingleAsync(cancellationToken);

        return new QcBatchDto(
            batch.Id, batch.GrnId, receipt.GrnNo, receipt.VendorDnNo, batch.PoId, batch.PoNo, batch.StatusId, batch.LotQty,
            batch.Lines.OrderBy(l => l.SizeId).ThenBy(l => l.ColourId).Select(l => new BatchLineDto(l.SizeId, l.ColourId, l.Qty)).ToList(),
            batch.Inspections.OrderBy(i => i.Id).Select(i => i.ToDto()).ToList(),
            batch.SubBatches.OrderBy(s => s.SplitNo).Select(s => s.ToDto()).ToList());
    }
}

/// <summary>Previews the AQL plan for a lot (spec AC-13): the same calculator the inspection uses, so the screen shows what will be applied.</summary>
public sealed record PreviewAqlPlanQuery(int LotQty, short? InspectionLevelId, short? AqlLevelId) : IRequest<AqlPlanDto>;

public sealed class PreviewAqlPlanQueryHandler(IQualityDbContext dbContext, QualityOptions options)
    : IRequestHandler<PreviewAqlPlanQuery, AqlPlanDto>
{
    public async Task<AqlPlanDto> Handle(PreviewAqlPlanQuery request, CancellationToken cancellationToken)
    {
        var table = await AqlPlanLoader.LoadAsync(dbContext, cancellationToken);
        var plan = AqlCalculator.Plan(
            table, request.LotQty,
            request.InspectionLevelId ?? PlanCodes.InspectionLevelId(options.DefaultInspectionLevelCode),
            request.AqlLevelId ?? PlanCodes.AqlLevelId(options.DefaultAqlLevelCode),
            options.FullInspectionMaxLot);
        return plan.ToDto(request.LotQty);
    }
}
