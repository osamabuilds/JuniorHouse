
namespace Romp.Modules.Production.Application.Abstractions;

/// <summary>"Configuration, not constants" (spec): the recorded-late threshold (D15), the at-risk buffer (D18) and the PP file limits (D7). Bound from <c>Prod:*</c>.</summary>
public sealed class ProductionOptions
{
    public int RecordedLateDays { get; init; } = 3;

    public int AtRiskBufferDays { get; init; } = 7;

    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;

    public int MaxFilesPerRound { get; init; } = 10;
}

/// <summary>"Today" for staff-entered dates is the Pakistan-time date (plan). Pakistan has no daylight saving, so a fixed +05:00 offset is exact and needs no tzdata.</summary>
public static class PakistanClock
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().ToOffset(Offset).DateTime);
}
