namespace Romp.Modules.Vendor.Domain.Vendors;

/// <summary>The counts one scorecard window is built from.</summary>
public sealed record ScorecardCounts(
    int DeliveriesCounted,
    int DeliveriesOnTime,
    int DeliveriesOnQuantity,
    int DeliveriesExcludedRompDelay,
    int InspectedUnits,
    int DefectiveUnits);

/// <summary>The measures; null means "no data", never 0% or 100% (spec AC-37).</summary>
public sealed record ScorecardMeasures(decimal? OnTimePct, decimal? OnQuantityPct, decimal? DefectRatePct);

/// <summary>
/// Pure formulas for the vendor scorecard (FR-SC-01, spec AC-35..AC-37, recommended default for Q2):
/// on-time % = on-time deliveries / deliveries counted, leaving out deliveries delayed by Romp's own late fabric dispatch
/// (so a Romp-caused delay never counts against the vendor); on-quantity % = on-quantity deliveries / deliveries counted
/// (short and over both count as not on quantity); defect rate % = defective units / inspected units, first-pass inspections
/// only. Percentages are rounded to one decimal place.
/// </summary>
public static class ScorecardCalculator
{
    public static ScorecardMeasures Calculate(ScorecardCounts counts)
    {
        var onTimeBase = counts.DeliveriesCounted - counts.DeliveriesExcludedRompDelay;
        return new ScorecardMeasures(
            Percent(counts.DeliveriesOnTime, onTimeBase),
            Percent(counts.DeliveriesOnQuantity, counts.DeliveriesCounted),
            Percent(counts.DefectiveUnits, counts.InspectedUnits));
    }

    private static decimal? Percent(int part, int whole) =>
        whole <= 0 ? null : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);
}
