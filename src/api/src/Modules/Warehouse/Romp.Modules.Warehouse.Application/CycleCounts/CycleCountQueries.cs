using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Domain;
using Romp.Modules.Warehouse.Domain.CycleCounts;

namespace Romp.Modules.Warehouse.Application.CycleCounts;

public sealed record CountBinDto(long BinId, string BinCode, bool Submitted);

/// <summary>What the counter submitted. There is deliberately no system quantity here (CC-2): the blind count.</summary>
public sealed record CountLineDto(long BinId, string BinCode, long VariantRefId, string SkuCode, string Gtin, short GradeId, string GradeCode, int CountedQuantity);

public sealed record CycleCountDto(
    long Id, string CountNo, short StatusId, string StatusCode, string CreatedBy, DateTimeOffset CreatedAt,
    IReadOnlyList<CountBinDto> Bins, IReadOnlyList<CountLineDto> Lines);

/// <summary>The review step (CC-4, CC-7): counted against system, flagged stale when the bin moved after the count opened.</summary>
public sealed record CountReviewLineDto(
    long BinId, string BinCode, long VariantRefId, string SkuCode, short GradeId, string GradeCode,
    int CountedQuantity, int SystemQuantity, int Variance, bool Stale, bool NotCounted);

public sealed record CountReviewDto(long CountId, string CountNo, bool AllBinsSubmitted, IReadOnlyList<CountReviewLineDto> Lines);

public sealed record CycleCountRowDto(long Id, string CountNo, short StatusId, string StatusCode, string CreatedBy, DateTimeOffset CreatedAt, int BinCount, int LineCount);

public sealed record BinAgeDto(long BinId, string BinCode, bool IsActive, DateTimeOffset? LastCountedAt, int? DaysSinceCounted, bool InOpenCount);

internal static class CycleCountViews
{
    public static string StatusCode(short id) => id switch
    {
        CountStatuses.Open => "OPEN",
        CountStatuses.Closed => "CLOSED",
        _ => "CANCELLED",
    };

    public static async Task<CycleCountDto> BuildAsync(IWarehouseDbContext dbContext, CycleCount count, CancellationToken cancellationToken)
    {
        var binIds = count.Bins.Select(b => b.BinId).ToList();
        var variantIds = count.Lines.Select(l => l.VariantRefId).Distinct().ToList();
        var binCodes = await dbContext.Bins.AsNoTracking().Where(b => binIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BinCode, cancellationToken);
        var variants = await dbContext.VariantRefs.AsNoTracking().Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, cancellationToken);

        return new CycleCountDto(
            count.Id, count.CountNo, count.StatusId, StatusCode(count.StatusId), count.CreatedBy, count.InsrDte,
            count.Bins.OrderBy(b => b.BinId).Select(b => new CountBinDto(b.BinId, binCodes[b.BinId], b.Submitted)).ToList(),
            count.Lines.OrderBy(l => l.BinId).ThenBy(l => l.VariantRefId).ThenBy(l => l.GradeId).Select(l => new CountLineDto(
                l.BinId, binCodes[l.BinId], l.VariantRefId, variants[l.VariantRefId].SkuCode, variants[l.VariantRefId].Gtin,
                l.GradeId, Grades.CodeOf(l.GradeId), l.CountedQuantity)).ToList());
    }

    public static async Task<CountReviewDto> ReviewAsync(IWarehouseDbContext dbContext, CycleCount count, CancellationToken cancellationToken)
    {
        var submitted = count.Bins.Where(b => b.Submitted).ToList();
        var binIds = submitted.Select(b => b.BinId).ToList();

        var system = await dbContext.Balances.AsNoTracking()
            .Where(b => binIds.Contains(b.BinId) && b.Quantity > 0)
            .Select(b => new { b.BinId, b.VariantRefId, b.GradeId, b.Quantity })
            .ToListAsync(cancellationToken);
        var systemByKey = system.ToDictionary(s => (s.BinId, s.VariantRefId, s.GradeId), s => s.Quantity);
        var countedByKey = count.Lines.Where(l => binIds.Contains(l.BinId)).ToDictionary(l => (l.BinId, l.VariantRefId, l.GradeId), l => l.CountedQuantity);

        var marks = await CountSupport.CurrentMarksAsync(dbContext, binIds, cancellationToken);
        var staleBins = submitted.Where(b => marks.GetValueOrDefault(b.BinId) > b.OpenMovementMark).Select(b => b.BinId).ToHashSet();

        var keys = systemByKey.Keys.Union(countedByKey.Keys).OrderBy(k => k.BinId).ThenBy(k => k.VariantRefId).ThenBy(k => k.GradeId).ToList();
        var variantIds = keys.Select(k => k.VariantRefId).Distinct().ToList();
        var variants = await dbContext.VariantRefs.AsNoTracking().Where(v => variantIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.SkuCode, cancellationToken);
        var binCodes = await dbContext.Bins.AsNoTracking().Where(b => binIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BinCode, cancellationToken);

        var lines = keys.Select(k =>
        {
            var counted = countedByKey.GetValueOrDefault(k);
            var sys = systemByKey.GetValueOrDefault(k);
            return new CountReviewLineDto(
                k.BinId, binCodes[k.BinId], k.VariantRefId, variants[k.VariantRefId], k.GradeId, Grades.CodeOf(k.GradeId),
                counted, sys, counted - sys, staleBins.Contains(k.BinId), !countedByKey.ContainsKey(k));
        }).ToList();

        return new CountReviewDto(count.Id, count.CountNo, count.Bins.All(b => b.Submitted), lines);
    }
}

/// <summary>One count, blind: bins and what has been counted so far, never the system quantity (CC-2).</summary>
public sealed record GetCycleCountQuery(long CountId) : IRequest<CycleCountDto>;

public sealed class GetCycleCountQueryHandler(IWarehouseDbContext dbContext) : IRequestHandler<GetCycleCountQuery, CycleCountDto>
{
    public async Task<CycleCountDto> Handle(GetCycleCountQuery request, CancellationToken cancellationToken)
    {
        var count = await dbContext.CycleCounts.AsNoTracking().Include(c => c.Bins).Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.CountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Count {request.CountId} was not found.");
        return await CycleCountViews.BuildAsync(dbContext, count, cancellationToken);
    }
}

/// <summary>The review step: system quantities appear here, and only here, after counts are submitted (CC-4). Needs the Inventory policy at the endpoint.</summary>
public sealed record ReviewCountQuery(long CountId) : IRequest<CountReviewDto>;

public sealed class ReviewCountQueryHandler(IWarehouseDbContext dbContext) : IRequestHandler<ReviewCountQuery, CountReviewDto>
{
    public async Task<CountReviewDto> Handle(ReviewCountQuery request, CancellationToken cancellationToken)
    {
        var count = await dbContext.CycleCounts.AsNoTracking().Include(c => c.Bins).Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.CountId, cancellationToken)
            ?? throw new KeyNotFoundException($"Count {request.CountId} was not found.");
        return await CycleCountViews.ReviewAsync(dbContext, count, cancellationToken);
    }
}

/// <summary>The counts list (CC-11): newest first, optionally one status. Ordered by the unique id; counts bins and lines in the same query (no N+1).</summary>
public sealed record SearchCycleCountsQuery(short? StatusId = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<CycleCountRowDto>>;

public sealed class SearchCycleCountsQueryValidator : AbstractValidator<SearchCycleCountsQuery>
{
    public SearchCycleCountsQueryValidator() =>
        RuleFor(q => q.StatusId).Must(s => s is null or CountStatuses.Open or CountStatuses.Closed or CountStatuses.Cancelled).WithMessage("Unknown count status.");
}

public sealed class SearchCycleCountsQueryHandler(IWarehouseDbContext dbContext) : IRequestHandler<SearchCycleCountsQuery, PagedResult<CycleCountRowDto>>
{
    public async Task<PagedResult<CycleCountRowDto>> Handle(SearchCycleCountsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.CycleCounts.AsNoTracking().AsQueryable();
        if (request.StatusId is { } status)
        {
            query = query.Where(c => c.StatusId == status);
        }

        var page = await query
            .OrderByDescending(c => c.Id)
            .Select(c => new { c.Id, c.CountNo, c.StatusId, c.CreatedBy, c.InsrDte, Bins = c.Bins.Count, Lines = c.Lines.Count })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var rows = page.Items.Select(c => new CycleCountRowDto(c.Id, c.CountNo, c.StatusId, CycleCountViews.StatusCode(c.StatusId), c.CreatedBy, c.InsrDte, c.Bins, c.Lines)).ToList();
        return new PagedResult<CycleCountRowDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>Days since each bin was last counted, to help choose the next bins to count (CC-11, decision D9). Never-counted bins come first.</summary>
public sealed record BinAgeQuery(string? Zone = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize) : IRequest<PagedResult<BinAgeDto>>;

public sealed class BinAgeQueryValidator : AbstractValidator<BinAgeQuery>
{
    public BinAgeQueryValidator() => RuleFor(q => q.Zone).MaximumLength(3);
}

public sealed class BinAgeQueryHandler(IWarehouseDbContext dbContext, TimeProvider timeProvider) : IRequestHandler<BinAgeQuery, PagedResult<BinAgeDto>>
{
    public async Task<PagedResult<BinAgeDto>> Handle(BinAgeQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var bins = dbContext.Bins.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Zone))
        {
            var zone = request.Zone.Trim().ToUpperInvariant();
            bins = bins.Where(b => b.Zone == zone);
        }

        var page = await bins
            .OrderBy(b => b.LastCountedAt == null ? 0 : 1).ThenBy(b => b.LastCountedAt).ThenBy(b => b.Id)
            .Select(b => new
            {
                b.Id, b.BinCode, b.IsActive, b.LastCountedAt,
                InOpenCount = dbContext.CountBins.Any(cb => cb.BinId == b.Id && cb.IsOpen),
            })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var rows = page.Items.Select(b => new BinAgeDto(
            b.Id, b.BinCode, b.IsActive, b.LastCountedAt, b.LastCountedAt is { } at ? (int)(now - at).TotalDays : null, b.InOpenCount)).ToList();
        return new PagedResult<BinAgeDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}
