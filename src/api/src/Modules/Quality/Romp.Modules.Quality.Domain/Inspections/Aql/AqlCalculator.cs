using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Quality.Domain.Inspections.Aql;

/// <summary>Accept and reject numbers for one code letter at one AQL level (ISO 2859-1 Table 2-A, single sampling, normal inspection).</summary>
public sealed record AqlLimit(short AqlLevelId, string CodeLetter, int AcceptNo, int RejectNo);

/// <summary>
/// The seeded AQL tables as the calculator sees them (spec AC-13). The Domain has no EF, so an implementation loads the
/// rows once and answers from memory; <see cref="AqlPlanTable"/> is that implementation.
/// </summary>
public interface IAqlPlanSource
{
    /// <summary>The sample-size code letter for a lot size at an inspection level, or null when the level has no plan (Table I).</summary>
    string? CodeLetterFor(short inspectionLevelId, int lotQty);

    /// <summary>The sample size that belongs to a code letter, or null for an unknown letter.</summary>
    int? SampleQtyFor(string codeLetter);

    /// <summary>The defined limits of an AQL level, in ascending sample-size order. Cells the standard marks with an arrow are not stored; the calculator resolves them.</summary>
    IReadOnlyList<AqlLimit> LimitsFor(short aqlLevelId);
}

/// <summary>The plan for one inspection: 100% or AQL sampling, the sample size and the accept/reject numbers.</summary>
public sealed record AqlPlan(short ModeId, string CodeLetter, int SampleQty, int AcceptNo, int RejectNo);

/// <summary>
/// Pure calculator (spec AC-13..AC-15): lot size, inspection level and AQL level in; sample size and accept/reject
/// numbers out, entirely from the seeded tables.
/// <list type="bullet">
/// <item>A lot no larger than <c>fullInspectionMaxLot</c> is inspected 100% (sample = lot, AC-14); the plan's accept/reject
/// numbers then apply to the whole lot, which is stricter than sampling, never looser.</item>
/// <item>Where the standard shows an arrow, the plan of the nearest defined code letter is used, including its own sample size
/// (a down arrow before the first defined cell, an up arrow after the last).</item>
/// <item>The sample never exceeds the lot and is at least 1 (AC-15).</item>
/// </list>
/// </summary>
public static class AqlCalculator
{
    public static AqlPlan Plan(IAqlPlanSource source, int lotQty, short inspectionLevelId, short aqlLevelId, int fullInspectionMaxLot)
    {
        if (lotQty < 1)
        {
            throw new DomainException("A batch must contain at least one unit to be inspected.");
        }

        var letter = source.CodeLetterFor(inspectionLevelId, lotQty)
            ?? throw new DomainException("No AQL sampling plan is defined for that inspection level.");
        var limits = source.LimitsFor(aqlLevelId);
        if (limits.Count == 0)
        {
            throw new DomainException("No AQL sampling plan is defined for that AQL level.");
        }

        var lotSample = SampleOf(source, letter);
        var resolved = limits.FirstOrDefault(l => l.CodeLetter == letter)
            ?? (lotSample < SampleOf(source, limits[0].CodeLetter) ? limits[0] : limits[^1]);

        var planSample = SampleOf(source, resolved.CodeLetter);
        return lotQty <= fullInspectionMaxLot
            ? new AqlPlan(InspectionMode.Full, resolved.CodeLetter, lotQty, resolved.AcceptNo, resolved.RejectNo)
            : new AqlPlan(InspectionMode.Aql, resolved.CodeLetter, Math.Clamp(planSample, 1, lotQty), resolved.AcceptNo, resolved.RejectNo);
    }

    private static int SampleOf(IAqlPlanSource source, string letter) =>
        source.SampleQtyFor(letter) ?? throw new DomainException($"The AQL plan has no sample size for code letter {letter}.");
}

/// <summary>An in-memory <see cref="IAqlPlanSource"/> built from the seeded rows.</summary>
public sealed class AqlPlanTable : IAqlPlanSource
{
    private readonly IReadOnlyList<AqlSampleRow> _samples;
    private readonly Dictionary<string, int> _sampleByLetter;
    private readonly ILookup<short, AqlLimitRow> _limits;

    public AqlPlanTable(IReadOnlyList<AqlSampleRow> samples, IReadOnlyList<AqlLimitRow> limits)
    {
        _samples = samples;
        _sampleByLetter = samples.GroupBy(s => s.CodeLetter).ToDictionary(g => g.Key, g => g.First().SampleQty);
        _limits = limits.ToLookup(l => l.AqlLevelId);
    }

    public string? CodeLetterFor(short inspectionLevelId, int lotQty) =>
        _samples.FirstOrDefault(s => s.InspectionLevelId == inspectionLevelId && lotQty >= s.LotMinQty && lotQty <= s.LotMaxQty)?.CodeLetter;

    public int? SampleQtyFor(string codeLetter) => _sampleByLetter.TryGetValue(codeLetter, out var qty) ? qty : null;

    public IReadOnlyList<AqlLimit> LimitsFor(short aqlLevelId) =>
        _limits[aqlLevelId]
            .Select(l => new AqlLimit(l.AqlLevelId, l.CodeLetter, l.AcceptNo, l.RejectNo))
            .OrderBy(l => _sampleByLetter.GetValueOrDefault(l.CodeLetter))
            .ToList();
}
