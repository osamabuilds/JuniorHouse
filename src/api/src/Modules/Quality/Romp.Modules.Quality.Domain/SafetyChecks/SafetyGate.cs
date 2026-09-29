using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Domain.Inspections;

namespace Romp.Modules.Quality.Domain.SafetyChecks;

/// <summary>
/// The child-safety gate (BRD 5.11, non-negotiable; spec AC-31, AC-32, AC-34). Pure rule: no quantity is Accepted or
/// accepted with a Concession unless both safety checks were recorded on the inspection round that covers it, and a quantity
/// on a line a check failed can only be Rejected or sent for Rework. <b>There is no override</b>: nothing here takes a flag,
/// role or setting that lets a decision past the gate, and there is deliberately no such parameter to add later without
/// changing this file and its tests.
/// </summary>
public static class SafetyGate
{
    private static readonly Dictionary<short, string> Names = new()
    {
        [SafetyType.ChokingHazardTrims] = "choking hazard on small trims",
        [SafetyType.DrawstringNeckline] = "drawstring safety on necklines",
    };

    /// <summary>
    /// Refuses a release decision that the safety results do not allow. <paramref name="inspection"/> is the latest round
    /// that covers the decided quantity (a rework return opens a new round that starts with no results, AC-34), and
    /// <paramref name="decisionLines"/> are the size x colour lines the decision covers.
    /// </summary>
    public static void EnsureCanRelease(short outcomeId, Inspection? inspection, IReadOnlyCollection<LineRef> decisionLines)
    {
        // Rejecting or reworking never puts a garment on sale, so it is always allowed.
        if (outcomeId is not (QcOutcome.Accepted or QcOutcome.Concession))
        {
            return;
        }

        if (inspection is null)
        {
            throw new DomainException("Record an inspection, including both child-safety checks, before accepting this quantity.");
        }

        foreach (var type in SafetyType.All)
        {
            var check = inspection.SafetyChecks.FirstOrDefault(s => s.SafetyTypeId == type)
                ?? throw new DomainException($"Record the child-safety check for {Names[type]} before accepting this quantity.");

            if (check.ResultId != CheckResult.Fail)
            {
                continue;
            }

            var blocked = check.AffectedLines.Count == 0
                ? decisionLines.ToList() // no lines listed: the whole batch failed the check
                : decisionLines.Where(l => check.AffectedLines.Any(a => a.SizeId == l.SizeId && a.ColourId == l.ColourId)).ToList();
            if (blocked.Count > 0)
            {
                var where = check.AffectedLines.Count == 0 ? "the whole batch" : $"size {blocked[0].SizeId} / colour {blocked[0].ColourId}";
                throw new DomainException(
                    $"The child-safety check for {Names[type]} failed for {where}, so that quantity can only be rejected or sent back for rework.");
            }
        }
    }
}
