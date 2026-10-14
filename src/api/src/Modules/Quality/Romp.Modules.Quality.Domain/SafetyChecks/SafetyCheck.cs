using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Quality.Domain.SafetyChecks;

/// <summary>A size and colour, as a failed safety check names it.</summary>
public sealed record LineRef(short SizeId, short ColourId);

/// <summary>One child-safety check result as entered (spec AC-31..AC-33).</summary>
public sealed record SafetyInput(short SafetyTypeId, short ResultId, string? Reason, IReadOnlyList<LineRef> AffectedLines);

/// <summary>
/// Maps to QC.QC_INSP_SAFE: one of the two mandatory child-safety checks of an inspection round (BRD 5.11: choking
/// hazard on small trims, drawstring safety on necklines). <see cref="Reason"/> is required for Not applicable (AC-33).
/// </summary>
public sealed class SafetyCheck : Entity<long>, IAuditable
{
    private readonly List<SafetyAffectedLine> _affectedLines = [];

    private SafetyCheck()
    {
        ActingPerson = string.Empty;
    }

    internal SafetyCheck(SafetyInput input, string actingPerson)
    {
        SafetyTypeId = input.SafetyTypeId;
        ActingPerson = actingPerson;
        Apply(input, actingPerson);
    }

    public long InspectionId { get; private set; }

    public short SafetyTypeId { get; private set; }

    public short ResultId { get; private set; }

    public string? Reason { get; private set; }

    public string ActingPerson { get; private set; }

    /// <summary>The size x colour lines a failed check applies to. Empty on a failure means the whole batch (Q10).</summary>
    public IReadOnlyCollection<SafetyAffectedLine> AffectedLines => _affectedLines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>Sets the result in place (a correction before any decision), keeping the affected lines in step without deleting and re-inserting identical rows.</summary>
    internal void Apply(SafetyInput input, string actingPerson)
    {
        ResultId = input.ResultId;
        Reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
        ActingPerson = actingPerson;

        var wanted = input.ResultId == CheckResult.Fail ? input.AffectedLines.Distinct().ToList() : [];
        _affectedLines.RemoveAll(existing => !wanted.Contains(new LineRef(existing.SizeId, existing.ColourId)));
        foreach (var line in wanted.Where(w => !_affectedLines.Any(e => e.SizeId == w.SizeId && e.ColourId == w.ColourId)))
        {
            _affectedLines.Add(new SafetyAffectedLine(line.SizeId, line.ColourId));
        }
    }
}

/// <summary>Maps to QC.QC_INSP_SAFE_LINE.</summary>
public sealed class SafetyAffectedLine : Entity<long>, IAuditable
{
    private SafetyAffectedLine()
    {
    }

    internal SafetyAffectedLine(short sizeId, short colourId)
    {
        SizeId = sizeId;
        ColourId = colourId;
    }

    public long SafetyCheckId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
