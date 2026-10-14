using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Application.Abstractions;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Inspections.Aql;

namespace Romp.Modules.Quality.Application.Inspections.Aql;

/// <summary>Loads the seeded AQL tables (about a hundred rows) into the in-memory <see cref="AqlPlanTable"/> the pure calculator reads (spec AC-13).</summary>
public static class AqlPlanLoader
{
    public static async Task<AqlPlanTable> LoadAsync(IQualityDbContext dbContext, CancellationToken cancellationToken)
    {
        var samples = await dbContext.AqlSamples.AsNoTracking().ToListAsync(cancellationToken);
        var limits = await dbContext.AqlLimits.AsNoTracking().ToListAsync(cancellationToken);
        return new AqlPlanTable(samples, limits);
    }
}

/// <summary>Maps the stable lookup codes staff and configuration use to the ids the tables store.</summary>
public static class PlanCodes
{
    private static readonly Dictionary<string, short> InspectionLevels = new(StringComparer.Ordinal)
    {
        ["I"] = InspectionLevel.I, ["II"] = InspectionLevel.II, ["III"] = InspectionLevel.III,
        ["S1"] = InspectionLevel.S1, ["S2"] = InspectionLevel.S2, ["S3"] = InspectionLevel.S3, ["S4"] = InspectionLevel.S4,
    };

    private static readonly Dictionary<string, short> AqlLevels = new(StringComparer.Ordinal)
    {
        ["Aql0_65"] = AqlLevel.Aql065, ["Aql1_0"] = AqlLevel.Aql100, ["Aql1_5"] = AqlLevel.Aql150,
        ["Aql2_5"] = AqlLevel.Aql250, ["Aql4_0"] = AqlLevel.Aql400, ["Aql6_5"] = AqlLevel.Aql650,
    };

    public static short InspectionLevelId(string code) =>
        InspectionLevels.TryGetValue(code, out var id) ? id : throw new DomainException($"'{code}' is not a known inspection level.");

    public static short AqlLevelId(string code) =>
        AqlLevels.TryGetValue(code, out var id) ? id : throw new DomainException($"'{code}' is not a known AQL level.");
}
