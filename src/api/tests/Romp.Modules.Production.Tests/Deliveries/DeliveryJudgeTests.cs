using Romp.Modules.Production.Domain.Deliveries;

namespace Romp.Modules.Production.Tests.Deliveries;

public sealed class DeliveryJudgeTests
{
    private static readonly DateOnly Expected = new(2026, 10, 15);
    private static readonly DateOnly Latest = new(2026, 10, 25);

    private static DeliveryTerms Terms(decimal? over, decimal? under, int ordered = 100, DateOnly? latest = null) =>
        new(0, Expected, latest ?? Latest, over, under, [new OrderedLine(1, 10, ordered)]);

    private static DeliveryJudgement Judge(
        decimal? over, decimal? under, int before, int inNote, bool final = false, int ordered = 100, DateOnly? dispatch = null, DateOnly? latest = null) =>
        DeliveryJudge.Judge(
            Terms(over, under, ordered, latest),
            before == 0 ? new Dictionary<(short, short), int>() : new Dictionary<(short, short), int> { [(1, 10)] = before },
            [new NoteLineInput(1, 10, inNote)],
            dispatch ?? Expected, final);

    [Theory]
    [Trait("Spec", "AC-21")]
    [InlineData(100, 5, 105)]   // exactly at the over tolerance: allowed
    [InlineData(100, 0, 100)]   // zero tolerance
    [InlineData(100, null, 100)] // no tolerance recorded is treated as 0%
    [InlineData(99, 5, 103)]    // 99 x 1.05 = 103.95, whole pieces only: floor
    [InlineData(10, 10, 11)]
    public void MaxAllowed_IsFlooredWholePieces(int ordered, int? overPercent, int expected) =>
        Assert.Equal(expected, DeliveryJudge.MaxAllowed(ordered, (decimal?)overPercent));

    [Theory]
    [Trait("Spec", "AC-24")]
    [InlineData(100, 5, 95)]
    [InlineData(100, 0, 100)]
    [InlineData(100, null, 100)]
    [InlineData(99, 5, 95)]     // 99 x 0.95 = 94.05, ceiling: 95
    [InlineData(10, 10, 9)]
    public void MinRequired_IsCeilingWholePieces(int ordered, int? underPercent, int expected) =>
        Assert.Equal(expected, DeliveryJudge.MinRequired(ordered, (decimal?)underPercent));

    [Fact]
    [Trait("Spec", "AC-24")]
    public void Judge_ReachesMinimumOnFirstNote_PoIsDelivered()
    {
        var result = Judge(over: 5, under: 5, before: 0, inNote: 95);

        Assert.True(result.PoIsDelivered);
        Assert.Equal(DeliveryQuantityStatus.OnQuantity, result.QuantityStatusId);
        Assert.Equal(0, result.ShortQty);
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public void Judge_OneBelowMinimum_NotFinal_PartiallyDeliveredAndNotShort()
    {
        var result = Judge(over: 5, under: 5, before: 0, inNote: 94);

        Assert.False(result.PoIsDelivered);
        Assert.Equal(DeliveryQuantityStatus.OnQuantity, result.QuantityStatusId); // still outstanding, not short yet
        Assert.Equal(0, result.ShortQty);
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public void Judge_SecondNoteCompletesTheOrder_UsesTheCumulativeTotal()
    {
        var result = Judge(over: 5, under: 5, before: 60, inNote: 35);

        Assert.True(result.PoIsDelivered);
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    [Trait("Spec", "D3")]
    public void Judge_FinalShipmentBelowMinimum_DeliveredAndShortByTheMissingPieces()
    {
        var result = Judge(over: 5, under: 5, before: 60, inNote: 20, final: true);

        Assert.True(result.PoIsDelivered);
        Assert.Equal(DeliveryQuantityStatus.Shortage, result.QuantityStatusId);
        Assert.Equal(15, result.ShortQty); // minimum 95, shipped 80
        Assert.Equal(15, Assert.Single(result.Lines).ShortQty);
    }

    [Fact]
    [Trait("Spec", "AC-21")]
    public void Judge_ExactlyAtOverTolerance_IsNotFlagged()
    {
        var result = Judge(over: 5, under: 5, before: 0, inNote: 105);

        Assert.Equal(DeliveryQuantityStatus.OnQuantity, result.QuantityStatusId);
        Assert.Equal(0, result.OverQty);
    }

    [Fact]
    [Trait("Spec", "AC-21")]
    [Trait("Spec", "D4")]
    public void Judge_OneOverTheTolerance_FlaggedOverByOne_NeverRefused()
    {
        var result = Judge(over: 5, under: 5, before: 0, inNote: 106);

        Assert.Equal(DeliveryQuantityStatus.Over, result.QuantityStatusId);
        Assert.Equal(1, result.OverQty);
        Assert.True(result.PoIsDelivered);
    }

    [Fact]
    [Trait("Spec", "AC-21")]
    public void Judge_OverAcrossNotes_OnlyThePiecesBeyondAllowedAreAttributedToTheLatestNote()
    {
        var result = Judge(over: 5, under: 5, before: 100, inNote: 10);

        Assert.Equal(5, result.OverQty); // cumulative 110 vs allowed 105
    }

    [Theory]
    [Trait("Spec", "AC-22")]
    [InlineData("2026-10-14", DeliveryTiming.OnTime, 0)]
    [InlineData("2026-10-15", DeliveryTiming.OnTime, 0)]
    [InlineData("2026-10-16", DeliveryTiming.Late, 1)]
    [InlineData("2026-10-25", DeliveryTiming.Late, 10)]
    [InlineData("2026-10-26", DeliveryTiming.BeyondAcceptable, 11)]
    public void Judge_Timing_IsJudgedOnTheDispatchDate(string dispatch, short timing, int daysLate)
    {
        var result = Judge(over: 5, under: 5, before: 0, inNote: 95, dispatch: DateOnly.Parse(dispatch, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(timing, result.TimingId);
        Assert.Equal(daysLate, result.DaysLate);
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Judge_NoLatestAcceptableDate_OnlyLateNeverBeyondAcceptable()
    {
        var terms = new DeliveryTerms(0, Expected, null, 5, 5, [new OrderedLine(1, 10, 100)]);
        var result = DeliveryJudge.Judge(terms, new Dictionary<(short, short), int>(), [new NoteLineInput(1, 10, 95)], new DateOnly(2026, 12, 1), false);

        Assert.Equal(DeliveryTiming.Late, result.TimingId);
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public void Judge_MultipleLines_AllMustReachTheirMinimum()
    {
        var terms = new DeliveryTerms(0, Expected, Latest, 5, 5, [new OrderedLine(1, 10, 100), new OrderedLine(2, 10, 50)]);

        var partial = DeliveryJudge.Judge(terms, new Dictionary<(short, short), int>(), [new NoteLineInput(1, 10, 100)], Expected, false);
        Assert.False(partial.PoIsDelivered); // line 2 has nothing yet

        var complete = DeliveryJudge.Judge(terms, new Dictionary<(short, short), int> { [(1, 10)] = 100 }, [new NoteLineInput(2, 10, 48)], Expected, false);
        Assert.True(complete.PoIsDelivered); // 48 >= ceil(50 x 0.95) = 48
    }
}
