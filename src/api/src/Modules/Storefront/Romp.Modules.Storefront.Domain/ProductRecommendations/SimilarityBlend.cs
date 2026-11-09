namespace Romp.Modules.Storefront.Domain.ProductRecommendations;

/// <summary>Mixes the field, meaning and sales scores of a candidate into one 0..1 score (REC-3, REC-13, ADR 0029).</summary>
public static class SimilarityBlend
{
    /// <summary>
    /// Weighted average of the parts. A null part is unavailable (for example no meaning provider): it is dropped and the other weights
    /// are shared out in proportion, so the result stays in 0..1. Weights must be non-negative with at least one positive.
    /// </summary>
    public static double Blend(IReadOnlyList<double?> parts, IReadOnlyList<double> weights)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(weights);
        if (parts.Count != weights.Count)
        {
            throw new ArgumentException("Parts and weights must have the same length.", nameof(weights));
        }

        Validate(weights);

        var total = 0.0;
        var used = 0.0;
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i] is not { } part || double.IsNaN(part))
            {
                continue;
            }

            total += Math.Clamp(part, 0, 1) * weights[i];
            used += weights[i];
        }

        return used <= 0 ? 0 : Math.Clamp(total / used, 0, 1);
    }

    /// <summary>Rejects negative, non-finite or all-zero weights (also used by the startup check on the options).</summary>
    public static void Validate(IReadOnlyList<double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (weights.Any(w => w < 0 || double.IsNaN(w) || double.IsInfinity(w)))
        {
            throw new ArgumentException("Weights must be finite and not negative.", nameof(weights));
        }

        if (!weights.Any(w => w > 0))
        {
            throw new ArgumentException("At least one weight must be positive.", nameof(weights));
        }
    }
}
