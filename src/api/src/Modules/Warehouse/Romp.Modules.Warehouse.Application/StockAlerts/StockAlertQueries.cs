using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Application.Paging;
using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Domain.StockAlerts;

namespace Romp.Modules.Warehouse.Application.StockAlerts;

/// <summary>
/// The alerts list (ALR-8): open alerts by default, newest first, filterable by level and by acknowledged state; <c>IncludeClosed</c>
/// shows history. Ordered by the unique id; one COUNT and one page query joined to the variant.
/// </summary>
public sealed record SearchAlertsQuery(short? LevelId = null, bool? Acknowledged = null, bool IncludeClosed = false, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<AlertRowDto>>;

public sealed class SearchAlertsQueryValidator : AbstractValidator<SearchAlertsQuery>
{
    public SearchAlertsQueryValidator() =>
        RuleFor(q => q.LevelId).Must(l => l is null || AlertLevels.IsKnown(l.Value)).WithMessage("Unknown alert level.");
}

public sealed class SearchAlertsQueryHandler(IWarehouseDbContext dbContext) : IRequestHandler<SearchAlertsQuery, PagedResult<AlertRowDto>>
{
    public async Task<PagedResult<AlertRowDto>> Handle(SearchAlertsQuery request, CancellationToken cancellationToken)
    {
        var alerts = dbContext.Alerts.AsNoTracking().AsQueryable();
        if (!request.IncludeClosed)
        {
            alerts = alerts.Where(a => a.StatusId == AlertStatuses.Open);
        }

        if (request.LevelId is { } level)
        {
            alerts = alerts.Where(a => a.LevelId == level);
        }

        if (request.Acknowledged is { } acknowledged)
        {
            alerts = acknowledged ? alerts.Where(a => a.AcknowledgedAt != null) : alerts.Where(a => a.AcknowledgedAt == null);
        }

        var page = await (
                from a in alerts
                join v in dbContext.VariantRefs.AsNoTracking() on a.VariantRefId equals v.Id
                orderby a.Id descending
                select new { a.Id, a.VariantRefId, v.SkuCode, v.Gtin, a.LevelId, a.LastQuantity, a.Threshold, a.OpenedAt, a.AcknowledgedBy, a.AcknowledgedAt })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var rows = page.Items.Select(a => new AlertRowDto(
            a.Id, a.VariantRefId, a.SkuCode, a.Gtin, a.LevelId, AlertLevels.CodeOf(a.LevelId), a.LastQuantity, a.Threshold, a.OpenedAt,
            a.AcknowledgedAt != null, a.AcknowledgedBy, a.AcknowledgedAt)).ToList();
        return new PagedResult<AlertRowDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}

/// <summary>The navigation badge (ALR-8): how many alerts are open and how many of those nobody has acknowledged. One statement.</summary>
public sealed record CountOpenAlertsQuery : IRequest<OpenAlertCountDto>;

public sealed class CountOpenAlertsQueryHandler(IWarehouseDbContext dbContext) : IRequestHandler<CountOpenAlertsQuery, OpenAlertCountDto>
{
    public async Task<OpenAlertCountDto> Handle(CountOpenAlertsQuery request, CancellationToken cancellationToken)
    {
        var counts = await dbContext.Alerts.AsNoTracking()
            .Where(a => a.StatusId == AlertStatuses.Open)
            .GroupBy(_ => 1)
            .Select(g => new { Open = g.Count(), Unacknowledged = g.Count(a => a.AcknowledgedAt == null) })
            .FirstOrDefaultAsync(cancellationToken);
        return new OpenAlertCountDto(counts?.Open ?? 0, counts?.Unacknowledged ?? 0);
    }
}

/// <summary>
/// Variants that have held stock, each with the threshold that applies to it (own override, or the configured default) and its current
/// available quantity (ALR-6), so the screen needs no per-row queries. Ordered by the unique variant id.
/// </summary>
public sealed record SearchThresholdsQuery(string? Search = null, int Page = 1, int PageSize = PageRequest.DefaultPageSize)
    : IRequest<PagedResult<ThresholdRowDto>>;

public sealed class SearchThresholdsQueryValidator : AbstractValidator<SearchThresholdsQuery>
{
    public SearchThresholdsQueryValidator() => RuleFor(q => q.Search).MaximumLength(ValidationRules.MaxSearchLength);
}

public sealed class SearchThresholdsQueryHandler(IWarehouseDbContext dbContext, IOptions<WarehouseOptions> options)
    : IRequestHandler<SearchThresholdsQuery, PagedResult<ThresholdRowDto>>
{
    public async Task<PagedResult<ThresholdRowDto>> Handle(SearchThresholdsQuery request, CancellationToken cancellationToken)
    {
        var variants = dbContext.VariantRefs.AsNoTracking().Where(v => dbContext.Movements.Any(m => m.VariantRefId == v.Id));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var text = request.Search.Trim();
            var upper = text.ToUpperInvariant();
            variants = variants.Where(v => v.SkuCode.Contains(upper) || v.Gtin == text);
        }

        var defaultThreshold = options.Value.DefaultLowStockThreshold;
        var page = await variants
            .OrderBy(v => v.Id)
            .Select(v => new
            {
                v.Id, v.SkuCode, v.Gtin,
                Available = dbContext.Balances.Where(b => b.VariantRefId == v.Id).Sum(b => (int?)b.Quantity) ?? 0,
                Override = dbContext.Thresholds.Where(t => t.VariantRefId == v.Id).Select(t => (int?)t.Quantity).FirstOrDefault(),
            })
            .ToPagedResultAsync(new PageRequest(request.Page, request.PageSize), cancellationToken);

        var rows = page.Items.Select(v => new ThresholdRowDto(v.Id, v.SkuCode, v.Gtin, v.Available, v.Override ?? defaultThreshold, v.Override is not null)).ToList();
        return new PagedResult<ThresholdRowDto>(rows, page.Page, page.PageSize, page.TotalCount);
    }
}
