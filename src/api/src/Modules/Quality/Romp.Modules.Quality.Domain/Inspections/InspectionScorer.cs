namespace Romp.Modules.Quality.Domain.Inspections;

/// <summary>The scored inspection: defect counts by severity, defective units, and the computed result (a <see cref="InspectionResult"/> id).</summary>
public sealed record InspectionScore(short ResultId, int CriticalCount, int MajorCount, int MinorCount, int DefectiveQty);

/// <summary>
/// Pure rule for the AQL result (spec AC-18, recommended default for Q1): Major and Minor defects are counted together
/// against the plan (Pass when the count is at or below the accept number, Fail when at or above the reject number), and
/// Critical defects have their own tolerance (zero by default: any critical defect fails the sample). The result is
/// computed, never typed in.
/// </summary>
public static class InspectionScorer
{
    public static InspectionScore Score(IReadOnlyList<DefectInput> defects, int sampleQty, int acceptNo, int rejectNo, int criticalTolerance)
    {
        var critical = defects.Where(d => d.SeverityId == DefectSeverity.Critical).Sum(d => d.Qty);
        var major = defects.Where(d => d.SeverityId == DefectSeverity.Major).Sum(d => d.Qty);
        var minor = defects.Where(d => d.SeverityId == DefectSeverity.Minor).Sum(d => d.Qty);
        var countable = major + minor;

        var fail = critical > criticalTolerance || countable >= rejectNo;
        var defective = Math.Min(critical + major + minor, sampleQty);
        return new InspectionScore(fail ? InspectionResult.Fail : InspectionResult.Pass, critical, major, minor, defective);
    }
}
