using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Inspections.Aql;

namespace Romp.Modules.Quality.Tests.Inspections;

public sealed class AqlCalculatorTests
{
    private static readonly AqlPlanTable Table = new(AqlSeedData.SampleRows(), AqlSeedData.LimitRows());

    // lot, inspection level, AQL level, expected code letter, sample, accept, reject. The full-inspection threshold is 50.
    public static TheoryData<int, short, short, string, int, int, int> Anchors => new()
    {
        // General level II, AQL 2.5: the series every garment inspector knows (lot 501-1200 -> sample 80, accept 5).
        { 60, InspectionLevel.II, AqlLevel.Aql250, "E", 13, 0, 1 },
        { 100, InspectionLevel.II, AqlLevel.Aql250, "F", 20, 1, 2 },
        { 200, InspectionLevel.II, AqlLevel.Aql250, "G", 32, 2, 3 },
        { 400, InspectionLevel.II, AqlLevel.Aql250, "H", 50, 3, 4 },
        { 1000, InspectionLevel.II, AqlLevel.Aql250, "J", 80, 5, 6 },
        { 2000, InspectionLevel.II, AqlLevel.Aql250, "K", 125, 7, 8 },
        { 5000, InspectionLevel.II, AqlLevel.Aql250, "L", 200, 10, 11 },
        { 20000, InspectionLevel.II, AqlLevel.Aql250, "M", 315, 14, 15 },
        { 100000, InspectionLevel.II, AqlLevel.Aql250, "N", 500, 21, 22 },
        // Up arrow: past the last defined cell the last plan applies, with its own (smaller) sample size.
        { 300000, InspectionLevel.II, AqlLevel.Aql250, "N", 500, 21, 22 },
        { 600000, InspectionLevel.II, AqlLevel.Aql250, "N", 500, 21, 22 },
        // Other AQL levels on the same lot.
        { 1000, InspectionLevel.II, AqlLevel.Aql400, "J", 80, 7, 8 },
        { 1000, InspectionLevel.II, AqlLevel.Aql150, "J", 80, 3, 4 },
        { 1000, InspectionLevel.II, AqlLevel.Aql100, "J", 80, 2, 3 },
        { 1000, InspectionLevel.II, AqlLevel.Aql650, "J", 80, 10, 11 },
        { 1000, InspectionLevel.II, AqlLevel.Aql065, "J", 80, 1, 2 },
        { 60, InspectionLevel.II, AqlLevel.Aql400, "E", 13, 1, 2 }, // AQL 4.0 starts one letter earlier (D: 0/1), so E is the next step
        // Down arrow: a code letter above the first defined cell uses the first defined plan, with its own sample size.
        { 60, InspectionLevel.II, AqlLevel.Aql100, "G", 32, 0, 1 },
        { 100, InspectionLevel.II, AqlLevel.Aql065, "H", 50, 0, 1 },
        // Other inspection levels.
        { 1000, InspectionLevel.I, AqlLevel.Aql250, "G", 32, 2, 3 },
        { 1000, InspectionLevel.III, AqlLevel.Aql250, "K", 125, 7, 8 },
    };

    [Theory]
    [MemberData(nameof(Anchors))]
    [Trait("Spec", "AC-13")]
    public void Plan_Table(int lot, short level, short aql, string letter, int sample, int accept, int reject)
    {
        var plan = AqlCalculator.Plan(Table, lot, level, aql, fullInspectionMaxLot: 50);

        Assert.Equal((letter, sample, accept, reject), (plan.CodeLetter, plan.SampleQty, plan.AcceptNo, plan.RejectNo));
        Assert.Equal(InspectionMode.Aql, plan.ModeId);
    }

    public static TheoryData<int, string> LotRangeEdges => new()
    {
        { 1, "A" }, { 8, "A" }, { 9, "B" }, { 15, "B" }, { 16, "C" }, { 25, "C" }, { 26, "D" }, { 50, "D" }, { 51, "E" }, { 90, "E" },
        { 91, "F" }, { 150, "F" }, { 151, "G" }, { 280, "G" }, { 281, "H" }, { 500, "H" }, { 501, "J" }, { 1200, "J" }, { 1201, "K" },
        { 3200, "K" }, { 3201, "L" }, { 10000, "L" }, { 10001, "M" }, { 35000, "M" }, { 35001, "N" }, { 150000, "N" },
        { 150001, "P" }, { 500000, "P" }, { 500001, "Q" },
    };

    [Theory]
    [MemberData(nameof(LotRangeEdges))]
    [Trait("Spec", "AC-13")]
    public void Table1_LotRangeEdges_GiveTheLevelIiCodeLetter(int lot, string letter)
    {
        Assert.Equal(letter, Table.CodeLetterFor(InspectionLevel.II, lot));
    }

    [Theory]
    [Trait("Spec", "AC-14")]
    [InlineData(1, InspectionMode.Full)]
    [InlineData(50, InspectionMode.Full)]
    [InlineData(51, InspectionMode.Aql)]
    [InlineData(1000, InspectionMode.Aql)]
    public void Plan_LotAtOrBelowThreshold_IsFullInspection(int lot, short expectedMode)
    {
        var plan = AqlCalculator.Plan(Table, lot, InspectionLevel.II, AqlLevel.Aql250, fullInspectionMaxLot: 50);

        Assert.Equal(expectedMode, plan.ModeId);
        if (expectedMode == InspectionMode.Full)
        {
            Assert.Equal(lot, plan.SampleQty);
        }
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public void Plan_ThresholdIsConfigurable()
    {
        var plan = AqlCalculator.Plan(Table, 300, InspectionLevel.II, AqlLevel.Aql250, fullInspectionMaxLot: 500);

        Assert.Equal(InspectionMode.Full, plan.ModeId);
        Assert.Equal(300, plan.SampleQty);
    }

    [Theory]
    [Trait("Spec", "AC-15")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(60)]
    [InlineData(500)]
    [InlineData(700000)]
    public void Plan_NeverExceedsLot_AndIsAtLeastOne(int lot)
    {
        foreach (var level in new[] { InspectionLevel.I, InspectionLevel.II, InspectionLevel.III })
        {
            foreach (var aql in new[] { AqlLevel.Aql065, AqlLevel.Aql100, AqlLevel.Aql150, AqlLevel.Aql250, AqlLevel.Aql400, AqlLevel.Aql650 })
            {
                var plan = AqlCalculator.Plan(Table, lot, level, aql, fullInspectionMaxLot: 0);

                Assert.InRange(plan.SampleQty, 1, lot);
                Assert.Equal(plan.AcceptNo + 1, plan.RejectNo);
            }
        }
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public void Plan_LevelWithoutPlan_Refused()
    {
        Assert.Throws<DomainException>(() => AqlCalculator.Plan(Table, 100, InspectionLevel.S1, AqlLevel.Aql250, 50));
        Assert.Throws<DomainException>(() => AqlCalculator.Plan(Table, 0, InspectionLevel.II, AqlLevel.Aql250, 50));
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public void SeedData_NoGapsOrOverlapsPerLevel_AndLadderIsMonotonic()
    {
        var samples = AqlSeedData.SampleRows();
        foreach (var level in new[] { InspectionLevel.I, InspectionLevel.II, InspectionLevel.III })
        {
            var ranges = samples.Where(s => s.InspectionLevelId == level).OrderBy(s => s.LotMinQty).ToList();
            Assert.Equal(15, ranges.Count);
            Assert.Equal(1, ranges[0].LotMinQty);
            Assert.Equal(int.MaxValue, ranges[^1].LotMaxQty);
            for (var i = 1; i < ranges.Count; i++)
            {
                Assert.Equal(ranges[i - 1].LotMaxQty + 1, ranges[i].LotMinQty);
            }
        }

        foreach (var aql in AqlSeedData.LimitRows().GroupBy(l => l.AqlLevelId))
        {
            var ac = aql.Select(l => l.AcceptNo).ToList();
            Assert.Equal(ac.Order(), ac);
            Assert.Equal(0, ac[0]);
            Assert.All(aql, l => Assert.Equal(l.AcceptNo + 1, l.RejectNo));
        }

        Assert.Equal(samples.Select(s => s.Id).Count(), samples.Select(s => s.Id).Distinct().Count());
        Assert.Equal(AqlSeedData.LimitRows().Count, AqlSeedData.LimitRows().Select(l => l.Id).Distinct().Count());
    }
}
