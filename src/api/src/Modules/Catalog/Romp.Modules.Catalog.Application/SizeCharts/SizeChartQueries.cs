using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Domain.SizeCharts;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.SizeCharts;

/// <summary>
/// The Catalog side of <see cref="ISizeChartQueries"/> (published-catalog task 5). A page of charts costs the same few queries however
/// many charts and lines it holds: the charts with their lines, then one lookup read per kind (PCT-8).
/// </summary>
public sealed class SizeChartQueries(ICatalogDbContext dbContext, ICatalogLookupQueries lookups) : ISizeChartQueries
{
    /// <summary>The most charts one call returns.</summary>
    public const int MaxPageSize = 200;

    public async Task<IReadOnlyList<PublishedSizeChart>> ListChartsAsync(long afterChartId, int take, CancellationToken cancellationToken)
    {
        var charts = await dbContext.Charts.AsNoTracking().AsSplitQuery()
            .Include(c => c.Lines)
            .Where(c => c.Id > afterChartId)
            .OrderBy(c => c.Id)
            .Take(Math.Clamp(take, 1, MaxPageSize))
            .ToListAsync(cancellationToken);
        return await BuildAsync(charts, cancellationToken);
    }

    public async Task<PublishedSizeChart?> GetChartAsync(short categoryId, short genderId, CancellationToken cancellationToken)
    {
        var unisex = await lookups.GetGenderIdsBySegmentAsync([ChartGate.UnisexSegment], cancellationToken);
        var fallback = unisex.TryGetValue(ChartGate.UnisexSegment, out var id) ? id : (short)0;
        var candidates = await dbContext.Charts.AsNoTracking().AsSplitQuery()
            .Include(c => c.Lines)
            .Where(c => c.CategoryId == categoryId && (c.GenderId == genderId || c.GenderId == fallback))
            .ToListAsync(cancellationToken);

        var chosen = ChartLookupRule.Choose(candidates, categoryId, genderId, fallback == 0 ? null : fallback, c => (c.CategoryId, c.GenderId));
        if (chosen is null)
        {
            return null;
        }

        return (await BuildAsync([chosen], cancellationToken))[0];
    }

    private async Task<IReadOnlyList<PublishedSizeChart>> BuildAsync(List<SizeChart> charts, CancellationToken cancellationToken)
    {
        if (charts.Count == 0)
        {
            return [];
        }

        var categories = await lookups.GetCategoriesAsync(charts.Select(c => c.CategoryId).Distinct().ToList(), cancellationToken);
        var genders = await lookups.GetGendersAsync(charts.Select(c => c.GenderId).Distinct().ToList(), cancellationToken);
        var sizes = await lookups.GetSizesAsync(charts.SelectMany(c => c.Lines).Select(l => l.SizeId).Distinct().ToList(), cancellationToken);

        return charts.Select(c => new PublishedSizeChart(
            c.Id,
            c.ContentVersion,
            Code(categories, c.CategoryId, "category"),
            Code(genders, c.GenderId, "gender"),
            c.Name,
            c.Lines.OrderBy(l => l.SizeId).Select(l => new PublishedSizeChartLine(
                Code(sizes, l.SizeId, "size"),
                l.AgeMinMonths, l.AgeMaxMonths, l.HeightMinCm, l.HeightMaxCm, l.WeightMinKg, l.WeightMaxKg, l.ChestCm, l.WaistCm, l.LengthCm)).ToList())).ToList();
    }

    private static PublishedCode Code(IReadOnlyDictionary<short, ApparelCode> map, short id, string kind) =>
        map.TryGetValue(id, out var code)
            ? new PublishedCode(code.Id, code.Code, code.Name)
            : throw new InvalidOperationException($"The {kind} with id {id.ToString(CultureInfo.InvariantCulture)} is not in the reference data.");
}
