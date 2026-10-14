using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.Adjustments;

namespace Romp.Modules.Warehouse.Application.Adjustments;

public sealed record AdjustmentReasonDto(short Id, string Code, string Name);

/// <summary>The active reason codes, in sort order (ADJ-2). A lookup, so more codes need a row, not a deployment (D12).</summary>
public sealed record ListAdjustmentReasonsQuery : IRequest<IReadOnlyList<AdjustmentReasonDto>>;

public sealed class ListAdjustmentReasonsQueryHandler(IWarehouseDbContext dbContext)
    : IRequestHandler<ListAdjustmentReasonsQuery, IReadOnlyList<AdjustmentReasonDto>>
{
    public async Task<IReadOnlyList<AdjustmentReasonDto>> Handle(ListAdjustmentReasonsQuery request, CancellationToken cancellationToken) =>
        await dbContext.AdjustmentReasons.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortSeq)
            .Select(r => new AdjustmentReasonDto(r.Id, r.Code, r.Name))
            .ToListAsync(cancellationToken);
}

/// <summary>
/// The adjustment history (ADJ-10): newest first, filterable by reason, variant and date range. Ordered by the unique id. One COUNT and
/// one page query joined to variant, bin and the ledger's before/after, however many rows the page holds.
/// </summary>
public sealed record SearchAdjustmentsQuery(
    short? ReasonId = null, long? VariantRefId = null, DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<AdjustmentDto>>;

public sealed class SearchAdjustmentsQueryValidator : AbstractValidator<SearchAdjustmentsQuery>
{
    public SearchAdjustmentsQueryValidator()
    {
        RuleFor(q => q.ReasonId).Must(r => r is null || AdjustmentReasons.IsKnown(r.Value)).WithMessage("Unknown reason.");
        RuleFor(q => q).Must(q => q.From is null || q.To is null || q.From <= q.To).WithName("To").WithMessage("The end date cannot be before the start date.");
    }
}

public sealed class SearchAdjustmentsQueryHandler(IWarehouseDbContext dbContext)
    : IRequestHandler<SearchAdjustmentsQuery, PagedResult<AdjustmentDto>>
{
    public async Task<PagedResult<AdjustmentDto>> Handle(SearchAdjustmentsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Adjustments.AsNoTracking().AsQueryable();
        if (request.ReasonId is { } reason)
        {
            query = query.Where(a => a.ReasonId == reason);
        }

        if (request.VariantRefId is { } variantId)
        {
            query = query.Where(a => a.VariantRefId == variantId);
        }

        if (request.From is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5)).ToUniversalTime(); // Pakistan day start (+05:00), sent as UTC
            query = query.Where(a => a.InsrDte >= start);
        }

        if (request.To is { } to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(5)).ToUniversalTime();
            query = query.Where(a => a.InsrDte < end);
        }

        var page = await (
                from a in query
                join v in dbContext.VariantRefs.AsNoTracking() on a.VariantRefId equals v.Id
                join b in dbContext.Bins.AsNoTracking() on a.BinId equals b.Id
                join m in dbContext.Movements.AsNoTracking() on new { Source = a.Id, Kind = MovementKinds.Adjustment } equals new { Source = m.SourceId, Kind = m.KindId }
                orderby a.Id descending
                select new
                {
                    a.Id, a.VariantRefId, v.SkuCode, v.Gtin, a.BinId, b.BinCode, a.GradeId, a.QuantityChange, a.ReasonId, a.Note, a.Reconfirmed,
                    m.BalanceBefore, m.BalanceAfter, a.ActorName, a.InsrDte,
                })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var rows = page.Items.Select(a => new AdjustmentDto(
            a.Id, a.VariantRefId, a.SkuCode, a.Gtin, a.BinId, a.BinCode, a.GradeId, Grades.CodeOf(a.GradeId), a.QuantityChange, a.ReasonId,
            AdjustmentReasons.CodeOf(a.ReasonId), a.Note, a.Reconfirmed, a.BalanceBefore, a.BalanceAfter, a.ActorName, a.InsrDte)).ToList();
        return new PagedResult<AdjustmentDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}
