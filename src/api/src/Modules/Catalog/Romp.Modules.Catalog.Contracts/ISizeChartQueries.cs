namespace Romp.Modules.Catalog.Contracts;

/// <summary>One row of a size chart: a size with its age, height and weight ranges and optional chest, waist and length, all in cm / kg / months.</summary>
public sealed record PublishedSizeChartLine(
    PublishedCode Size,
    int? AgeMinMonths,
    int? AgeMaxMonths,
    decimal? HeightMinCm,
    decimal? HeightMaxCm,
    decimal? WeightMinKg,
    decimal? WeightMaxKg,
    decimal? ChestCm,
    decimal? WaistCm,
    decimal? LengthCm);

/// <summary>
/// A size chart for a category and gender, as one full snapshot. <c>Version</c> comes from one sequence shared by all charts, so it never
/// repeats and a chart deleted and created again for the same category and gender always has a higher one (PCT-5).
/// </summary>
public sealed record PublishedSizeChart(
    long ChartId,
    long Version,
    PublishedCode Category,
    PublishedCode Gender,
    string Name,
    IReadOnlyList<PublishedSizeChartLine> Lines);

/// <summary>
/// The Catalog contract the storefront reads size charts through (FR-08). Paged by chart id so a rebuild walks every chart in a constant
/// number of queries per page.
/// </summary>
public interface ISizeChartQueries
{
    /// <summary>Charts with a higher id than <paramref name="afterChartId"/>, ordered by id, each exactly as stored (no fallback applied).</summary>
    Task<IReadOnlyList<PublishedSizeChart>> ListChartsAsync(long afterChartId, int take, CancellationToken cancellationToken);

    /// <summary>
    /// The chart a product of this category and gender shows (CHT-3): the category's chart for that gender, else its Unisex chart, else
    /// null. When the answer's <c>Gender</c> is not the one asked for, the asked gender has no chart of its own.
    /// </summary>
    Task<PublishedSizeChart?> GetChartAsync(short categoryId, short genderId, CancellationToken cancellationToken);
}
