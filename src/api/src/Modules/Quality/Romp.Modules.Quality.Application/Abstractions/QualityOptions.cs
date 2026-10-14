namespace Romp.Modules.Quality.Application.Abstractions;

/// <summary>"Configuration, not constants" (spec Q1, AC-46): the AQL defaults and the guards, bound from <c>Qc:*</c>.</summary>
public sealed class QualityOptions
{
    /// <summary>Lots up to this size are inspected 100% (BRD 5.11: "unless volume is small enough to inspect 100%").</summary>
    public int FullInspectionMaxLot { get; init; } = 50;

    public string DefaultInspectionLevelCode { get; init; } = "II";

    public string DefaultAqlLevelCode { get; init; } = "Aql2_5";

    /// <summary>Critical defects tolerated in a sample before it fails (zero: any critical defect fails).</summary>
    public int CriticalDefectTolerance { get; init; }

    /// <summary>How many days ahead of today (Pakistan time) a receipt date may be.</summary>
    public int ReceiptFutureDays { get; init; }
}

/// <summary>"Today" for staff-entered dates is the Pakistan-time date. Pakistan has no daylight saving, so a fixed +05:00 offset is exact and needs no tzdata.</summary>
public static class PakistanClock
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(Offset).DateTime);
}
