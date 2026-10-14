using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.InboundDeliveries;
using Romp.Modules.Quality.Domain.Receiving;

namespace Romp.Modules.Quality.Tests.Receiving;

public sealed class ReconciliationJudgeTests
{
    public static TheoryData<string, int[], short> Cases => new()
    {
        // name, claimed/counted pairs flattened [claimed1, counted1, claimed2, counted2, ...], expected status
        { "exact", [100, 100, 50, 50], GrnReconciliation.Matched },
        { "one line short", [100, 90, 50, 50], GrnReconciliation.Shortage },
        { "one line over", [100, 100, 50, 55], GrnReconciliation.Over },
        { "mixed, fewer counted in total", [100, 80, 50, 55], GrnReconciliation.Shortage },
        { "mixed, more counted in total", [100, 95, 50, 70], GrnReconciliation.Over },
        { "mixed, equal totals is conservative Short", [100, 90, 50, 60], GrnReconciliation.Shortage },
        { "nothing counted", [100, 0, 50, 0], GrnReconciliation.Shortage },
        { "unclaimed line counted", [0, 10], GrnReconciliation.Over },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Spec", "AC-7")]
    public void Judge_Table(string name, int[] pairs, short expected)
    {
        var lines = new List<ReconciliationLine>();
        for (var i = 0; i < pairs.Length; i += 2)
        {
            lines.Add(new ReconciliationLine((short)(i / 2 + 1), 10, pairs[i], pairs[i + 1]));
        }

        var result = ReconciliationJudge.Judge(lines);

        Assert.True(expected == result.StatusId, $"{name}: expected {GrnReconciliation.Code(expected)} but was {GrnReconciliation.Code(result.StatusId)}");
        Assert.Equal(lines.Sum(l => l.ClaimedQty), result.ClaimedTotal);
        Assert.Equal(lines.Sum(l => l.CountedQty), result.CountedTotal);
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public void Line_DiffAndExcess_AreCountedMinusClaimed()
    {
        var over = new ReconciliationLine(1, 10, 100, 104);
        var under = new ReconciliationLine(1, 10, 100, 96);

        Assert.Equal(4, over.DiffQty);
        Assert.True(over.IsExcess);
        Assert.Equal(-4, under.DiffQty);
        Assert.False(under.IsExcess);
    }
}

public sealed class GoodsReceiptTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private static readonly IReadOnlySet<(short SizeId, short ColourId)> PoLines = new HashSet<(short, short)> { (1, 10), (2, 10), (3, 10) };

    private static InboundDelivery Delivery(DateOnly? dispatch = null) =>
        new(runId: 1, poId: 7, poNo: "PO-2026-00007", vendorDnNo: "DN-1", dispatch ?? new DateOnly(2026, 9, 20), false,
            [new ClaimedLine(1, 10, 100), new ClaimedLine(2, 10, 50)]);

    private static GoodsReceipt Open(
        InboundDelivery delivery, IReadOnlyList<CountedLine>? counted = null, DateOnly? received = null, string person = "Sara Khan") =>
        GoodsReceipt.Open(delivery, "GRN-2026-00001", received ?? Today, person, null,
            counted ?? [new CountedLine(1, 10, 100), new CountedLine(2, 10, 50)], PoLines, Today, futureDays: 0);

    [Fact]
    [Trait("Spec", "AC-3")]
    public void Open_Valid_StoresPoDnCountsAndPerson()
    {
        var grn = Open(Delivery());

        Assert.Equal("GRN-2026-00001", grn.GrnNo);
        Assert.Equal("PO-2026-00007", grn.PoNo);
        Assert.Equal("DN-1", grn.VendorDnNo);
        Assert.Equal("Sara Khan", grn.ActingPerson);
        Assert.Equal(GrnReconciliation.Matched, grn.ReconciliationId);
        Assert.Equal(150, grn.CountedTotal);
        Assert.Equal(2, grn.Lines.Count);
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public void Open_CancelledPo_Refused()
    {
        var delivery = Delivery();
        delivery.MarkCancelled();

        var error = Assert.Throws<DomainException>(() => Open(delivery));

        Assert.Contains("cancelled", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void Open_LineNotOnDnOrPo_Refused()
    {
        var error = Assert.Throws<DomainException>(() => Open(Delivery(), [new CountedLine(1, 10, 100), new CountedLine(9, 99, 5)]));

        Assert.Contains("size 9", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void Open_LineOnPoButNotOnDn_RecordedAsExcess()
    {
        var grn = Open(Delivery(), [new CountedLine(1, 10, 100), new CountedLine(2, 10, 50), new CountedLine(3, 10, 12)]);

        var extra = grn.Lines.Single(l => l.SizeId == 3);
        Assert.Equal(0, extra.ClaimedQty);
        Assert.Equal(12, extra.CountedQty);
        Assert.True(extra.IsExcess);
        Assert.Equal(GrnReconciliation.Over, grn.ReconciliationId);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void Open_NegativeOrDuplicateLine_Refused()
    {
        Assert.Throws<DomainException>(() => Open(Delivery(), [new CountedLine(1, 10, -1)]));
        Assert.Throws<DomainException>(() => Open(Delivery(), [new CountedLine(1, 10, 5), new CountedLine(1, 10, 6)]));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public void Open_UncountedDnLine_RecordedAsZeroAndShort()
    {
        var grn = Open(Delivery(), [new CountedLine(1, 10, 100)]);

        var missing = grn.Lines.Single(l => l.SizeId == 2);
        Assert.Equal(0, missing.CountedQty);
        Assert.Equal(-50, missing.DiffQty);
        Assert.Equal(GrnReconciliation.Shortage, grn.ReconciliationId);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public void Open_DateBeforeDispatch_Refused()
    {
        Assert.Throws<DomainException>(() => Open(Delivery(dispatch: new DateOnly(2026, 9, 25)), received: new DateOnly(2026, 9, 24)));
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public void Open_FutureDate_Refused_AndDispatchDayAndTodayAllowed()
    {
        Assert.Throws<DomainException>(() => Open(Delivery(), received: Today.AddDays(1)));
        Open(Delivery(dispatch: new DateOnly(2026, 9, 25)), received: new DateOnly(2026, 9, 25));
        Open(Delivery(), received: Today);
    }

    [Theory]
    [Trait("Spec", "AC-8")]
    [Trait("Spec", "AC-30")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public void Open_NoActingPerson_Refused(string person)
    {
        Assert.Throws<DomainException>(() => Open(Delivery(), person: person));
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void RaiseRecorded_CarriesGrnPoAndLines()
    {
        var grn = Open(Delivery(), [new CountedLine(1, 10, 90), new CountedLine(2, 10, 50)]);

        grn.RaiseRecorded();

        var raised = Assert.IsType<Romp.Modules.Quality.Contracts.GoodsReceiptRecordedEvent>(Assert.Single(grn.DomainEvents));
        Assert.Equal("Short", raised.ReconciliationCode);
        Assert.Equal(150, raised.ClaimedTotal);
        Assert.Equal(140, raised.CountedTotal);
        Assert.Equal(2, raised.Lines.Count);
    }
}
