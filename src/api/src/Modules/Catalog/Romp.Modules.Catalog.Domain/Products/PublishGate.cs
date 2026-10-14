using Romp.Modules.Catalog.Domain.Urls;

namespace Romp.Modules.Catalog.Domain.Products;

/// <summary>One thing that stops a publish: a stable code, a plain-words message, and the editor tab that fixes it (PRD-4).</summary>
public sealed record GateFailure(string Code, string Message, string Tab);

/// <summary>The outcome of the publish gate: every failure at once (never just the first) and any advice that does not block.</summary>
public sealed record PublishGateResult(IReadOnlyList<GateFailure> Failures, IReadOnlyList<string> Warnings)
{
    public bool Passed => Failures.Count == 0;
}

/// <summary>Everything the gate needs, gathered by the publish handler from the product and from the other features' gate readers.</summary>
public sealed record PublishGateInput(
    string? Name,
    string? Description,
    short GenderId,
    short CategoryId,
    short AgeBracketId,
    string? Slug,
    int LabelledVariants,
    int LabelledUnpricedVariants,
    int ImageCount,
    IReadOnlyList<string> ImagesNeedingAltText,
    int CompositionRows,
    decimal CompositionTotalPercent,
    int CareInstructions,
    bool HasSizeChart);

/// <summary>
/// The publish gate in one pure place (PRD-4, PRD-13; module plan "Publish gate"). Variants still waiting for a GTIN do not block a
/// publish (D1); a product with no variant that has one does (Q-PRD-7). A missing size chart never blocks (CHT-3): it is a warning.
/// </summary>
public static class PublishGate
{
    public const string TabContent = "content";
    public const string TabVariants = "variants";
    public const string TabImages = "images";
    public const string TabComposition = "composition";
    public const string TabUrl = "url";

    public const string NoSizeChartWarning = "NO_SIZE_CHART";

    public static PublishGateResult Evaluate(PublishGateInput input, IReadOnlyCollection<string> reservedSlugs, int maxSlugLength = SlugPolicy.DefaultMaxLength)
    {
        var failures = new List<GateFailure>();

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            failures.Add(new GateFailure("NAME_MISSING", "Add a product name.", TabContent));
        }

        if (string.IsNullOrWhiteSpace(input.Description))
        {
            failures.Add(new GateFailure("DESCRIPTION_MISSING", "Add a product description.", TabContent));
        }

        if (input.GenderId <= 0)
        {
            failures.Add(new GateFailure("GENDER_MISSING", "Choose who the product is for (boys, girls or unisex).", TabContent));
        }

        if (input.CategoryId <= 0)
        {
            failures.Add(new GateFailure("CATEGORY_MISSING", "Choose a category.", TabContent));
        }

        if (input.AgeBracketId <= 0)
        {
            failures.Add(new GateFailure("AGE_MISSING", "Choose an age group.", TabContent));
        }

        if (input.LabelledVariants == 0)
        {
            failures.Add(new GateFailure("NO_SELLABLE_VARIANT", "No variant is available yet: Labelling has not issued a barcode for any size and colour.", TabVariants));
        }

        if (input.LabelledUnpricedVariants > 0)
        {
            failures.Add(new GateFailure(
                "VARIANT_UNPRICED",
                input.LabelledUnpricedVariants == 1
                    ? "One available variant has no price."
                    : $"{input.LabelledUnpricedVariants} available variants have no price.",
                TabVariants));
        }

        if (input.ImageCount == 0)
        {
            failures.Add(new GateFailure("NO_IMAGE", "Add at least one image.", TabImages));
        }

        foreach (var image in input.ImagesNeedingAltText)
        {
            failures.Add(new GateFailure("ALT_TEXT_MISSING", $"{image} needs alt text that describes it.", TabImages));
        }

        if (input.CompositionRows == 0 || input.CompositionTotalPercent != 100m)
        {
            failures.Add(new GateFailure(
                "COMPOSITION_TOTAL",
                input.CompositionRows == 0
                    ? "Add the fabric composition; it must total 100%."
                    : $"The fabric composition totals {input.CompositionTotalPercent:0.#}%; it must total exactly 100%.",
                TabComposition));
        }

        if (input.CareInstructions == 0)
        {
            failures.Add(new GateFailure("CARE_MISSING", "Choose at least one care instruction.", TabComposition));
        }

        var slugProblem = SlugPolicy.Validate(input.Slug, reservedSlugs, maxSlugLength);
        if (slugProblem is not null)
        {
            failures.Add(new GateFailure("SLUG_INVALID", slugProblem, TabUrl));
        }

        var warnings = input.HasSizeChart ? Array.Empty<string>() : [NoSizeChartWarning];
        return new PublishGateResult(failures, warnings);
    }
}
