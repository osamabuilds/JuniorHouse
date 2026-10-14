namespace Romp.Modules.Vendor.Contracts;

/// <summary>
/// The vendor scorecard (FR-SC-01, Sprint 4, spec AC-35..AC-39). Every measure is null when there is no data behind it (a vendor
/// with no deliveries or no inspections yet), never 0% or 100%. The counts are shown next to the percentages so a reader can
/// see how much data a number rests on. <c>WindowDays</c> is the length of the rolling window behind the <c>*Rolling</c>
/// measures; the other measures cover all time.
/// </summary>
public sealed record VendorScorecard(
    long VendorId,
    int WindowDays,
    int DeliveriesCounted,
    int DeliveriesOnTime,
    int DeliveriesOnQuantity,
    int DeliveriesExcludedRompDelay,
    int InspectedUnits,
    int DefectiveUnits,
    decimal? OnTimePct,
    decimal? OnQuantityPct,
    decimal? DefectRatePct,
    decimal? OnTimePctRolling,
    decimal? OnQuantityPctRolling,
    decimal? DefectRatePctRolling);

/// <summary>The read-only scorecard question other modules may ask (ADR 0002): they depend on this Contracts project only.</summary>
public interface IVendorScorecardQueries
{
    /// <summary>The scorecard of a vendor, or null for an unknown vendor.</summary>
    Task<VendorScorecard?> GetScorecardAsync(long vendorId, CancellationToken cancellationToken);
}
