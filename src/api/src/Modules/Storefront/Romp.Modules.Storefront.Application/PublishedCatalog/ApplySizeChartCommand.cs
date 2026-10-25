using MediatR;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Reference.Contracts;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Domain.PublishedCatalog;

namespace Romp.Modules.Storefront.Application.PublishedCatalog;

/// <summary>
/// Projects one size chart into the read model (PCT-1). A null <paramref name="Chart"/> means the chart no longer exists and removes
/// it with its lines. Charts are keyed by Catalog's chart id and versioned from one sequence, so the highest version wins; a chart
/// deleted and created again for the same category and gender (new id, higher version) replaces the old one whichever arrives first.
/// </summary>
public sealed record ApplySizeChartCommand(long ChartId, PublishedSizeChart? Chart) : IRequest<ProjectionOutcome>, IStorefrontCommand;

public sealed class ApplySizeChartCommandHandler(IStorefrontReadDbContext db, ICatalogLookupQueries lookups)
    : IRequestHandler<ApplySizeChartCommand, ProjectionOutcome>
{
    public async Task<ProjectionOutcome> Handle(ApplySizeChartCommand request, CancellationToken cancellationToken)
    {
        var chart = request.Chart;
        if (chart is not null && chart.ChartId != request.ChartId)
        {
            throw new InvalidOperationException($"Chart {chart.ChartId} was sent for chart {request.ChartId}.");
        }

        var row = await db.Charts.FirstOrDefaultAsync(c => c.Id == request.ChartId, cancellationToken);
        if (chart is null)
        {
            if (row is null)
            {
                return ProjectionOutcome.Absent;
            }

            db.Charts.Remove(row);
            return ProjectionOutcome.Removed;
        }

        if (chart.Version < 1)
        {
            throw new InvalidOperationException($"Chart {chart.ChartId} has no content version.");
        }

        if (row is not null && !VersionedSnapshot.Replaces(chart.Version, row.ContentVersion))
        {
            return ProjectionOutcome.Unchanged;
        }

        // At most one chart per category and gender: a different chart already holding the slot either is older (replaced) or newer (this one is stale).
        var rival = await db.Charts.AsNoTracking()
            .Where(c => c.Id != chart.ChartId && c.CategoryCode == chart.Category.Code && c.GenderCode == chart.Gender.Code)
            .Select(c => new { c.Id, c.ContentVersion })
            .FirstOrDefaultAsync(cancellationToken);
        if (rival is not null)
        {
            if (rival.ContentVersion > chart.Version)
            {
                return ProjectionOutcome.Unchanged;
            }

            await db.Charts.Where(c => c.Id == rival.Id).ExecuteDeleteAsync(cancellationToken);
        }

        var created = row is null;
        if (row is null)
        {
            row = new SizeChartRow(chart.ChartId, chart.Version, chart.Category.Code, chart.Category.Name, chart.Gender.Code, chart.Gender.Name, chart.Name);
            db.Charts.Add(row);
        }
        else
        {
            row.Replace(chart.Version, chart.Category.Code, chart.Category.Name, chart.Gender.Code, chart.Gender.Name, chart.Name);
            await db.ChartLines.Where(l => l.ChartId == chart.ChartId).ExecuteDeleteAsync(cancellationToken);
        }

        var order = (await lookups.ListSizesAsync(cancellationToken)).ToDictionary(l => l.Id, l => l.SortSeq);
        db.ChartLines.AddRange(chart.Lines.Select(l => new SizeChartLineRow(
            chart.ChartId, l.Size.Code, l.Size.Name, order.TryGetValue(l.Size.Id, out var seq) ? seq : short.MaxValue,
            l.AgeMinMonths, l.AgeMaxMonths, l.HeightMinCm, l.HeightMaxCm, l.WeightMinKg, l.WeightMaxKg, l.ChestCm, l.WaistCm, l.LengthCm)));
        return created ? ProjectionOutcome.Created : ProjectionOutcome.Updated;
    }
}
