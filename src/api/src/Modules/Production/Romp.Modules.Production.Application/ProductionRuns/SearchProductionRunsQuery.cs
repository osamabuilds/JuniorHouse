using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Production.Application.Abstractions;
using Romp.Modules.Production.Domain.Deliveries;

namespace Romp.Modules.Production.Application.ProductionRuns;

/// <summary>The paged production list (AC-32, CONVENTIONS section 4): one COUNT plus one page query, however many runs there are.</summary>
public sealed record SearchProductionRunsQuery(
    short? StatusId = null,
    string? PoNo = null,
    bool? AtRisk = null,
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<RunSummaryDto>>;

public sealed class SearchProductionRunsQueryHandler(IProductionDbContext dbContext, ProductionOptions options)
    : IRequestHandler<SearchProductionRunsQuery, PagedResult<RunSummaryDto>>
{
    public async Task<PagedResult<RunSummaryDto>> Handle(SearchProductionRunsQuery request, CancellationToken cancellationToken)
    {
        var buffer = options.AtRiskBufferDays;
        var query = dbContext.Runs.AsNoTracking().AsQueryable();

        if (request.StatusId is { } statusId)
        {
            query = query.Where(r => r.StatusId == statusId);
        }

        if (!string.IsNullOrWhiteSpace(request.PoNo))
        {
            // PO numbers are generated in upper case (PO-YYYY-NNNNN), so an upper-cased needle matches without a locale-sensitive SQL function.
            var text = request.PoNo.Trim().ToUpperInvariant();
            query = query.Where(r => r.PoNo.Contains(text));
        }

        if (request.AtRisk == true)
        {
            query = query.Where(r => r.ExpectedCompletionDate != null && r.LatestAcceptableDate != null
                && r.ExpectedCompletionDate > r.LatestAcceptableDate.Value.AddDays(-buffer));
        }

        // Everything below is computed in SQL from the run's own rows; no entity is loaded and nothing runs per row.
        return await query
            .OrderByDescending(r => r.Id)
            .Select(r => new RunSummaryDto(
                r.Id, r.PoId, r.PoNo, r.StatusId, r.FabricResponsibilityId,
                r.ExpectedCompletionDate, r.LatestAcceptableDate,
                r.ExpectedCompletionDate != null && r.LatestAcceptableDate != null
                    && r.ExpectedCompletionDate > r.LatestAcceptableDate.Value.AddDays(-buffer),
                r.CuttingStartedDate != null,
                r.PpRounds.Any(p => p.DecisionId == Domain.Samples.PpDecision.Approved),
                r.IsCuttingBeforeFabricDispatch, r.IsResampleRecommended, r.IsCuttingWithoutResample,
                r.DeliveryNotes.SelectMany(n => n.Lines).Sum(l => l.Qty),
                r.DeliveryNotes.Any(n => n.QuantityStatusId == DeliveryQuantityStatus.Over),
                r.DeliveryNotes.Any(n => n.QuantityStatusId == DeliveryQuantityStatus.Shortage),
                r.DeliveryNotes.Any(n => n.TimingId != DeliveryTiming.OnTime)))
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);
    }
}
