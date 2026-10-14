using Romp.Modules.Catalog.Domain.Products;

namespace Romp.Modules.Catalog.Tests.Products;

public sealed class PublishGateTests
{
    private static readonly string[] Reserved = ["cart", "admin"];

    private static PublishGateInput Complete() => new(
        "Floral Dress", "A dress.", 2, 3, 4, "floral-dress",
        LabelledVariants: 2, LabelledUnpricedVariants: 0,
        ImageCount: 2, ImagesNeedingAltText: [],
        CompositionRows: 2, CompositionTotalPercent: 100m, CareInstructions: 2, HasSizeChart: true);

    private static string[] Codes(PublishGateInput input) => PublishGate.Evaluate(input, Reserved).Failures.Select(f => f.Code).ToArray();

    [Fact]
    [Trait("Spec", "PRD-4")]
    public void ACompleteProduct_Passes_WithNoWarnings()
    {
        var result = PublishGate.Evaluate(Complete(), Reserved);

        Assert.True(result.Passed);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    [Trait("Spec", "PRD-4")]
    public void EveryMissingItem_IsNamed_AtOnce()
    {
        var input = new PublishGateInput(
            " ", "", 0, 0, 0, "Bad Slug",
            LabelledVariants: 0, LabelledUnpricedVariants: 0,
            ImageCount: 0, ImagesNeedingAltText: [],
            CompositionRows: 0, CompositionTotalPercent: 0m, CareInstructions: 0, HasSizeChart: false);

        var codes = Codes(input);

        Assert.Equal(
            ["NAME_MISSING", "DESCRIPTION_MISSING", "GENDER_MISSING", "CATEGORY_MISSING", "AGE_MISSING", "NO_SELLABLE_VARIANT", "NO_IMAGE", "COMPOSITION_TOTAL", "CARE_MISSING", "SLUG_INVALID"],
            codes);
    }

    [Fact]
    [Trait("Spec", "PRD-4")]
    public void EachFailure_PointsAtTheTabThatFixesIt()
    {
        var result = PublishGate.Evaluate(
            Complete() with { Name = null, ImageCount = 0, CareInstructions = 0, Slug = "cart", LabelledVariants = 0 }, Reserved);

        Assert.Equal("content", result.Failures.Single(f => f.Code == "NAME_MISSING").Tab);
        Assert.Equal("images", result.Failures.Single(f => f.Code == "NO_IMAGE").Tab);
        Assert.Equal("composition", result.Failures.Single(f => f.Code == "CARE_MISSING").Tab);
        Assert.Equal("url", result.Failures.Single(f => f.Code == "SLUG_INVALID").Tab);
        Assert.Equal("variants", result.Failures.Single(f => f.Code == "NO_SELLABLE_VARIANT").Tab);
    }

    [Fact]
    [Trait("Spec", "PRD-13")]
    public void SomeVariantsStillWaitingForAGtin_DoNotBlockThePublish()
    {
        // The gate counts only variants that have a GTIN; unlabelled ones are not in the count at all (D1).
        Assert.Empty(Codes(Complete() with { LabelledVariants = 1 }));
    }

    [Fact]
    [Trait("Spec", "PRD-8")]
    public void ALabelledVariantWithoutAPrice_BlocksThePublish()
    {
        var codes = Codes(Complete() with { LabelledUnpricedVariants = 2 });

        Assert.Equal(["VARIANT_UNPRICED"], codes);
    }

    [Fact]
    [Trait("Spec", "MED-1")]
    public void EachImageWithoutAltText_IsNamed()
    {
        var result = PublishGate.Evaluate(
            Complete() with { ImagesNeedingAltText = ["Image 2 (IMG_1234.jpg)", "Image 3 (back.png)"] }, Reserved);

        Assert.Equal(2, result.Failures.Count(f => f.Code == "ALT_TEXT_MISSING"));
        Assert.Contains(result.Failures, f => f.Message.Contains("IMG_1234.jpg", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(95.0, "COMPOSITION_TOTAL")]
    [InlineData(100.1, "COMPOSITION_TOTAL")]
    [InlineData(99.9, "COMPOSITION_TOTAL")]
    [Trait("Spec", "CMP-1")]
    public void ACompositionThatDoesNotTotalExactly100_Blocks(double total, string code) =>
        Assert.Contains(code, Codes(Complete() with { CompositionTotalPercent = (decimal)total }));

    [Fact]
    [Trait("Spec", "CHT-3")]
    public void AMissingSizeChart_IsAWarning_NeverAFailure()
    {
        var result = PublishGate.Evaluate(Complete() with { HasSizeChart = false }, Reserved);

        Assert.True(result.Passed);
        Assert.Equal([PublishGate.NoSizeChartWarning], result.Warnings);
    }
}
