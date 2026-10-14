using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Contracts;

namespace Romp.Modules.Catalog.Domain.SizeCharts;

/// <summary>
/// One row of a size chart: a size with its age range (months), height range (cm), weight range (kg) and optional chest, waist and
/// length (cm) (FR-08, CHT-4). Every minimum is at most its maximum, nothing is negative, and a row has an age range or at least one
/// measurement. Maps to CTLG.CHRT_LINE.
/// </summary>
public sealed class SizeChartLine : Entity<long>, IAuditable
{
    /// <summary>A guard against typing slips such as a height of 9,999 cm.</summary>
    public const decimal MaxCentimetres = 300m;
    public const decimal MaxKilograms = 150m;
    public const int MaxAgeMonths = 240;

    private SizeChartLine()
    {
    }

    public SizeChartLine(
        short sizeId,
        int? ageMinMonths,
        int? ageMaxMonths,
        decimal? heightMinCm,
        decimal? heightMaxCm,
        decimal? weightMinKg,
        decimal? weightMaxKg,
        decimal? chestCm,
        decimal? waistCm,
        decimal? lengthCm)
    {
        var problem = Check(ageMinMonths, ageMaxMonths, heightMinCm, heightMaxCm, weightMinKg, weightMaxKg, chestCm, waistCm, lengthCm);
        if (problem is not null)
        {
            throw new DomainException(problem);
        }

        SizeId = sizeId;
        AgeMinMonths = ageMinMonths;
        AgeMaxMonths = ageMaxMonths;
        HeightMinCm = heightMinCm;
        HeightMaxCm = heightMaxCm;
        WeightMinKg = weightMinKg;
        WeightMaxKg = weightMaxKg;
        ChestCm = chestCm;
        WaistCm = waistCm;
        LengthCm = lengthCm;
    }

    public long ChartId { get; private set; }

    public short SizeId { get; private set; }

    public int? AgeMinMonths { get; private set; }

    public int? AgeMaxMonths { get; private set; }

    public decimal? HeightMinCm { get; private set; }

    public decimal? HeightMaxCm { get; private set; }

    public decimal? WeightMinKg { get; private set; }

    public decimal? WeightMaxKg { get; private set; }

    public decimal? ChestCm { get; private set; }

    public decimal? WaistCm { get; private set; }

    public decimal? LengthCm { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>The first problem with a row's values, or null when it is acceptable (CHT-4). One source for the validator and the entity.</summary>
    public static string? Check(
        int? ageMin, int? ageMax, decimal? heightMin, decimal? heightMax, decimal? weightMin, decimal? weightMax, decimal? chest, decimal? waist, decimal? length)
    {
        if (ageMin is < 0 || ageMax is < 0 || ageMin > MaxAgeMonths || ageMax > MaxAgeMonths)
        {
            return $"Age must be between 0 and {MaxAgeMonths} months.";
        }

        if (ageMin is not null && ageMax is not null && ageMin > ageMax)
        {
            return "The youngest age cannot be above the oldest age.";
        }

        foreach (var value in new[] { heightMin, heightMax, chest, waist, length })
        {
            if (value is < 0 or > MaxCentimetres)
            {
                return $"Measurements must be between 0 and {MaxCentimetres:0} cm.";
            }
        }

        foreach (var value in new[] { weightMin, weightMax })
        {
            if (value is < 0 or > MaxKilograms)
            {
                return $"Weight must be between 0 and {MaxKilograms:0} kg.";
            }
        }

        if (heightMin is not null && heightMax is not null && heightMin > heightMax)
        {
            return "The smallest height cannot be above the largest height.";
        }

        if (weightMin is not null && weightMax is not null && weightMin > weightMax)
        {
            return "The lightest weight cannot be above the heaviest weight.";
        }

        var hasAge = ageMin is not null || ageMax is not null;
        var hasMeasurement = new[] { heightMin, heightMax, weightMin, weightMax, chest, waist, length }.Any(v => v is not null);
        return hasAge || hasMeasurement ? null : "Give each size an age range or at least one measurement.";
    }
}

/// <summary>
/// A size chart for a category and gender (FR-08, TC-PDP-01). A product uses its category's chart for its gender, else the category's
/// Unisex chart, else shows "no size chart" (CHT-3). One line per size (CHT-5). Maps to CTLG.CHRT_MAIN.
/// </summary>
public sealed class SizeChart : AggregateRoot<long>, IAuditable
{
    private readonly List<SizeChartLine> _lines = [];

    private SizeChart()
    {
        Name = string.Empty;
    }

    public SizeChart(short categoryId, short genderId, string name, IEnumerable<SizeChartLine> lines)
    {
        CategoryId = categoryId;
        GenderId = genderId;
        Name = name.Trim();
        SetLines(lines);
    }

    public short CategoryId { get; private set; }

    public short GenderId { get; private set; }

    public string Name { get; private set; }

    public IReadOnlyCollection<SizeChartLine> Lines => _lines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Replaces the chart's name and lines (CHT-2): one line per size, at least one line.</summary>
    public void Update(string name, IEnumerable<SizeChartLine> lines)
    {
        Name = name.Trim();
        SetLines(lines);
    }

    /// <summary>Writes the change event; called by the handler once the chart has an id (CHT-2).</summary>
    public void NoteChanged() => Raise(new SizeChartChangedEvent(Id, CategoryId, GenderId));

    private void SetLines(IEnumerable<SizeChartLine> lines)
    {
        var incoming = lines.ToList();
        if (incoming.Count == 0)
        {
            throw new DomainException("A size chart needs at least one size.");
        }

        if (incoming.Select(l => l.SizeId).Distinct().Count() != incoming.Count)
        {
            throw new DomainException("Each size can appear only once in a chart.");
        }

        _lines.Clear();
        _lines.AddRange(incoming);
    }
}

/// <summary>
/// The chart a product uses (CHT-1, CHT-3): the chart for its category and gender, else the category's chart for the fallback
/// ("unisex") gender, else none. Pure, so the same rule serves the product page and the publish gate.
/// </summary>
public static class ChartLookupRule
{
    public static T? Choose<T>(IEnumerable<T> candidates, short categoryId, short genderId, short? fallbackGenderId, Func<T, (short CategoryId, short GenderId)> key)
        where T : class
    {
        var forCategory = candidates.Where(c => key(c).CategoryId == categoryId).ToList();
        return forCategory.FirstOrDefault(c => key(c).GenderId == genderId)
            ?? (fallbackGenderId is { } fallback ? forCategory.FirstOrDefault(c => key(c).GenderId == fallback) : null);
    }
}
