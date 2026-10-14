using Microsoft.EntityFrameworkCore;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Reference.Contracts;

namespace Romp.Modules.Catalog.Application.SizeCharts;

/// <summary>The gate reader size-charts exposes. It never blocks a publish (CHT-3); it only reports whether a chart applies.</summary>
public interface IChartGate
{
    Task<bool> HasChartAsync(short categoryId, short genderId, CancellationToken cancellationToken);
}

/// <summary>A chart applies to a category and gender, else to the category's Unisex chart (the lookup rule).</summary>
public sealed class ChartGate(ICatalogDbContext dbContext, ICatalogLookupQueries lookups) : IChartGate
{
    public const string UnisexSegment = "unisex";

    public async Task<bool> HasChartAsync(short categoryId, short genderId, CancellationToken cancellationToken)
    {
        var unisex = await lookups.GetGenderIdsBySegmentAsync([UnisexSegment], cancellationToken);
        var fallback = unisex.TryGetValue(UnisexSegment, out var id) ? id : (short)0;
        return await dbContext.Charts.AsNoTracking()
            .AnyAsync(c => c.CategoryId == categoryId && (c.GenderId == genderId || c.GenderId == fallback), cancellationToken);
    }
}
