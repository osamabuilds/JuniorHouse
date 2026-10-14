using Romp.BuildingBlocks.Domain;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Catalog.Domain.SizeCharts;

namespace Romp.Modules.Catalog.Tests.SizeCharts;

public sealed class SizeChartTests
{
    private static SizeChartLine Line(short size = 4, int? ageMin = 12, int? ageMax = 24, decimal? hMin = 80, decimal? hMax = 92, decimal? wMin = null, decimal? wMax = null) =>
        new(size, ageMin, ageMax, hMin, hMax, wMin, wMax, null, null, null);

    [Fact]
    [Trait("Spec", "FR-08")]
    public void ALine_MayHaveAnAgeRangeAndOrMeasurements()
    {
        Assert.NotNull(Line(hMin: null, hMax: null));
        Assert.NotNull(new SizeChartLine(4, null, null, null, null, null, null, 52m, null, null));
    }

    [Fact]
    [Trait("Spec", "FR-08")]
    public void ALineWithNothingInIt_IsRefused() =>
        Assert.Throws<DomainException>(() => new SizeChartLine(4, null, null, null, null, null, null, null, null, null));

    [Theory]
    [InlineData(24, 12)]
    [Trait("Spec", "CHT-4")]
    public void AnAgeMinimumAboveItsMaximum_IsRefused(int min, int max) =>
        Assert.Throws<DomainException>(() => Line(ageMin: min, ageMax: max));

    [Fact]
    [Trait("Spec", "CHT-4")]
    public void AHeightOrWeightMinimumAboveItsMaximum_IsRefused()
    {
        Assert.Throws<DomainException>(() => Line(hMin: 100, hMax: 90));
        Assert.Throws<DomainException>(() => Line(wMin: 20, wMax: 10));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(301)]
    [Trait("Spec", "CHT-4")]
    public void ANegativeOrAbsurdMeasurement_IsRefused(double cm) =>
        Assert.Throws<DomainException>(() => Line(hMin: (decimal)cm, hMax: null));

    [Fact]
    [Trait("Spec", "CHT-4")]
    public void ANegativeAge_IsRefused() => Assert.Throws<DomainException>(() => Line(ageMin: -1, ageMax: 5));

    [Fact]
    [Trait("Spec", "CHT-5")]
    public void TwoLinesForTheSameSize_AreRefused() =>
        Assert.Throws<DomainException>(() => new SizeChart(3, 2, "Dresses", [Line(4), Line(4)]));

    [Fact]
    public void AChartNeedsAtLeastOneLine() =>
        Assert.Throws<DomainException>(() => new SizeChart(3, 2, "Dresses", []));

    [Fact]
    [Trait("Spec", "CHT-2")]
    public void UpdatingAChart_ReplacesItsLines()
    {
        var chart = new SizeChart(3, 2, "Dresses", [Line(4), Line(5)]);

        chart.Update(" Girls dresses ", [Line(6)]);

        Assert.Equal("Girls dresses", chart.Name);
        Assert.Equal(6, Assert.Single(chart.Lines).SizeId);
    }

    [Fact]
    [Trait("Spec", "CHT-2")]
    public void NoteChanged_RaisesTheChartChangedEvent()
    {
        var chart = new SizeChart(3, 2, "Dresses", [Line()]);

        chart.NoteChanged();

        var changed = Assert.IsType<SizeChartChangedEvent>(Assert.Single(chart.DomainEvents));
        Assert.Equal((short)3, changed.CategoryId);
        Assert.Equal((short)2, changed.GenderId);
    }

    private sealed record Candidate(short CategoryId, short GenderId, string Name);

    [Fact]
    [Trait("Spec", "CHT-1")]
    public void TheChartForAProduct_IsItsGenderThenTheCategorysUnisexChart()
    {
        const short unisex = 3;
        var girls = new Candidate(3, 2, "girls");
        var fallback = new Candidate(3, unisex, "unisex");
        var other = new Candidate(1, 2, "tops");
        Candidate[] all = [fallback, other, girls];

        Assert.Same(girls, ChartLookupRule.Choose(all, 3, 2, unisex, c => (c.CategoryId, c.GenderId)));
        Assert.Same(fallback, ChartLookupRule.Choose(all, 3, 1, unisex, c => (c.CategoryId, c.GenderId)));
    }

    [Fact]
    [Trait("Spec", "CHT-3")]
    public void WithoutAnyChartForTheCategory_ThereIsNone()
    {
        Candidate[] all = [new(1, 2, "tops")];

        Assert.Null(ChartLookupRule.Choose(all, 3, 2, 3, c => (c.CategoryId, c.GenderId)));
        Assert.Null(ChartLookupRule.Choose(all, 3, 2, null, c => (c.CategoryId, c.GenderId)));
    }
}
