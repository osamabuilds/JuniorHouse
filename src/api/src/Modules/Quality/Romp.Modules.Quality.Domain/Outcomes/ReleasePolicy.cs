namespace Romp.Modules.Quality.Domain.Outcomes;

/// <summary>
/// The one place that decides which outcomes put quantity on sale (spec AC-24, FR-SC-05): only <c>Accepted</c> and
/// <c>Concession</c> (the tag travels with the stock) are releasable. Rejected, Rework, undecided and pending quantity never
/// is. Everything that reports released quantity, in this module or through <c>Quality.Contracts</c>, asks this policy, so
/// there is no second definition to drift out of step.
/// </summary>
public static class ReleasePolicy
{
    /// <summary>The releasable outcome ids, for queries that filter in SQL.</summary>
    public static readonly IReadOnlyList<short> ReleasableOutcomes = [QcOutcome.Accepted, QcOutcome.Concession];

    public static bool IsReleasable(short? outcomeId) => outcomeId is { } id && ReleasableOutcomes.Contains(id);
}
