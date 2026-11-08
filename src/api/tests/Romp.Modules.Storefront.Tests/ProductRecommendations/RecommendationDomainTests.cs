using Romp.Modules.Storefront.Domain.ProductRecommendations;

namespace Romp.Modules.Storefront.Tests.ProductRecommendations;

/// <summary>Recommendations tasks R1 to R4: the pure scoring and the entity rules (REC-2, REC-3, REC-13).</summary>
public sealed class RecommendationDomainTests
{
    private static readonly double[] Equal = [1, 1, 1];

    [Fact]
    [Trait("Spec", "REC-3")]
    public void Blend_EqualWeights_AveragesTheParts() =>
        Assert.Equal(0.5, SimilarityBlend.Blend([0.2, 0.5, 0.8], Equal), 9);

    [Fact]
    [Trait("Spec", "REC-13")]
    public void Blend_NullPart_IsDroppedAndWeightsRenormalise()
    {
        var score = SimilarityBlend.Blend([1.0, null, 0.0], [0.5, 0.35, 0.15]);

        Assert.Equal(0.5 / 0.65, score, 9);
    }

    [Fact]
    [Trait("Spec", "REC-13")]
    public void Blend_AllPartsNull_IsZero() =>
        Assert.Equal(0, SimilarityBlend.Blend([null, null, null], Equal));

    [Fact]
    [Trait("Spec", "REC-13")]
    public void Blend_PartsOutsideRange_AreClamped() =>
        Assert.Equal(1.0, SimilarityBlend.Blend([5.0, 9.0, 2.0], Equal), 9);

    [Theory]
    [Trait("Spec", "REC-13")]
    [InlineData(-1, 1, 1)]
    [InlineData(0, 0, 0)]
    [InlineData(double.NaN, 1, 1)]
    public void Blend_BadWeights_AreRejected(double a, double b, double c) =>
        Assert.Throws<ArgumentException>(() => SimilarityBlend.Blend([1.0, 1.0, 1.0], [a, b, c]));

    [Fact]
    [Trait("Spec", "REC-2")]
    public void FieldScore_SameCategory_BeatsADifferentOne()
    {
        var a = new SimilarityProfile("DRESS", ["RED"], 2000m);
        var same = new SimilarityProfile("DRESS", ["BLUE"], 2000m);
        var other = new SimilarityProfile("SHIRT", ["BLUE"], 2000m);

        Assert.True(FieldSimilarity.Score(a, same) > FieldSimilarity.Score(a, other));
    }

    [Fact]
    [Trait("Spec", "REC-2")]
    public void ColourOverlap_IdenticalIsOne_DisjointIsZero()
    {
        Assert.Equal(1, FieldSimilarity.ColourOverlap(["RED", "BLUE"], ["blue", "red"]));
        Assert.Equal(0, FieldSimilarity.ColourOverlap(["RED"], ["BLUE"]));
        Assert.Equal(1.0 / 3, FieldSimilarity.ColourOverlap(["RED", "BLUE"], ["RED", "GREEN"]), 9);
    }

    [Fact]
    [Trait("Spec", "REC-2")]
    public void PriceCloseness_EqualIsOne_TenfoldIsZero()
    {
        Assert.Equal(1, FieldSimilarity.PriceCloseness(1500m, 1500m), 9);
        Assert.Equal(0, FieldSimilarity.PriceCloseness(100m, 1000m), 9);
        Assert.Equal(0, FieldSimilarity.PriceCloseness(100m, 5000m), 9);
        Assert.Equal(0, FieldSimilarity.PriceCloseness(null, 1000m));
    }

    [Fact]
    [Trait("Spec", "REC-2")]
    public void FieldScore_IsTheStatedMix()
    {
        var a = new SimilarityProfile("DRESS", ["RED"], 1000m);
        var identical = new SimilarityProfile("DRESS", ["RED"], 1000m);
        var nothingShared = new SimilarityProfile("SHIRT", ["BLUE"], 10_000m);

        Assert.Equal(1.0, FieldSimilarity.Score(a, identical), 9);
        Assert.Equal(0.0, FieldSimilarity.Score(a, nothingShared), 9);
        Assert.Equal(0.5, FieldSimilarity.Score(a, new SimilarityProfile("DRESS", ["BLUE"], 10_000m)), 9);
    }

    [Fact]
    [Trait("Spec", "REC-3")]
    public void Sales_BestSellerIsOne_NoSalesIsZero()
    {
        var scores = SalesSignal.Normalise(new Dictionary<long, int> { [1] = 100, [2] = 0, [3] = 10 });

        Assert.Equal(1, scores[1], 9);
        Assert.Equal(0, scores[2]);
        Assert.InRange(scores[3], 0.01, 0.99);
    }

    [Fact]
    [Trait("Spec", "REC-3")]
    public void Sales_NobodySold_AllZero() =>
        Assert.All(SalesSignal.Normalise(new Dictionary<long, int> { [1] = 0, [2] = 0 }).Values, v => Assert.Equal(0, v));

    [Fact]
    [Trait("Spec", "REC-3")]
    public void Sales_ScaleIsLogarithmic()
    {
        var scores = SalesSignal.Normalise(new Dictionary<long, int> { [1] = 100, [2] = 50 });

        Assert.True(scores[2] > 0.5, "half the units must score more than half");
    }

    [Theory]
    [Trait("Spec", "REC-2")]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void SimilarProduct_RankOutsideOneToTwelve_IsRejected(int rank) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimilarProduct(1, 2, rank, 0.5, DateTimeOffset.UtcNow));

    [Fact]
    [Trait("Spec", "REC-5")]
    public void SimilarProduct_ToItself_IsRejected() =>
        Assert.Throws<ArgumentException>(() => new SimilarProduct(7, 7, 1, 0.5, DateTimeOffset.UtcNow));

    [Fact]
    [Trait("Spec", "REC-4")]
    public void ProductSaleDay_NegativeUnits_AreRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProductSaleDay(1, new DateOnly(2026, 11, 1), -1));

    [Fact]
    [Trait("Spec", "REC-3")]
    public void RecommendationRun_UnfinishedUntilFinished()
    {
        var run = new RecommendationRun(DateTimeOffset.UtcNow);
        Assert.False(run.IsFinished);

        run.Finish(DateTimeOffset.UtcNow, 3, 40);

        Assert.True(run.IsFinished);
        Assert.Equal(40, run.ProductCount);
    }
}
