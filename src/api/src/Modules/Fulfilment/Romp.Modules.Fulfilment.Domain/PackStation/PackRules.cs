namespace Romp.Modules.Fulfilment.Domain.PackStation;

/// <summary>
/// Which packaging to use (PAK-4, Q-FUL-1): a poly bag for a small soft parcel (not more than <c>polyMaxItems</c> units and not more than
/// <c>polyMaxGrams</c> expected), otherwise a box. A pure function; the two numbers are settings.
/// </summary>
public static class PackagingRule
{
    public static short Recommend(int units, int expectedGrams, int polyMaxGrams, int polyMaxItems) =>
        units <= polyMaxItems && expectedGrams <= polyMaxGrams ? PackagingKinds.PolyBag : PackagingKinds.Box;
}

/// <summary>What weighing a parcel found (PAK-5 to PAK-8, FR-SC-12).</summary>
public sealed record WeightCheckResult(bool IsValid, bool IsWithinTolerance, int DeviationGrams, int AllowedGrams, string? Problem);

/// <summary>
/// Compares the weighed parcel with the expected weight (Q-FUL-2). A weight must be a whole number of grams above zero and not above 30 kg
/// (PAK-8). The weight is within tolerance when it differs by no more than the larger of the percentage and the fixed grams. A pure function.
/// </summary>
public static class WeightCheck
{
    public const int MaxGrams = 30_000;

    public static WeightCheckResult Evaluate(int actualGrams, int expectedGrams, int tolerancePercent, int toleranceGrams)
    {
        if (actualGrams <= 0)
        {
            return new WeightCheckResult(false, false, 0, 0, "The weight must be more than zero grams.");
        }

        if (actualGrams > MaxGrams)
        {
            return new WeightCheckResult(false, false, 0, 0, "A parcel over 30 kg cannot be right: check the scale.");
        }

        var allowed = Math.Max((int)Math.Ceiling(expectedGrams * tolerancePercent / 100.0), toleranceGrams);
        var deviation = actualGrams - expectedGrams;
        return new WeightCheckResult(true, Math.Abs(deviation) <= allowed, deviation, allowed, null);
    }
}
