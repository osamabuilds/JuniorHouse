using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Quality.Domain.SafetyChecks;

/// <summary>
/// Maps to QC.QC_INSP_SAFE: one of the two mandatory child-safety checks of an inspection round (BRD 5.11: choking
/// hazard on small trims, drawstring safety on necklines). <see cref="Reason"/> is required for Not applicable (AC-33).
/// </summary>
public sealed class SafetyCheck : Entity<long>, IAuditable
{
    private readonly List<SafetyAffectedLine> _affectedLines = [];

    private SafetyCheck()
    {
    }

    public long InspectionId { get; private set; }

    public short SafetyTypeId { get; private set; }

    public short ResultId { get; private set; }

    public string? Reason { get; private set; }

    /// <summary>The size x colour lines a failed check applies to. Empty on a failure means the whole batch (Q10).</summary>
    public IReadOnlyCollection<SafetyAffectedLine> AffectedLines => _affectedLines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to QC.QC_INSP_SAFE_LINE.</summary>
public sealed class SafetyAffectedLine : Entity<long>, IAuditable
{
    private SafetyAffectedLine()
    {
    }

    public long SafetyCheckId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
