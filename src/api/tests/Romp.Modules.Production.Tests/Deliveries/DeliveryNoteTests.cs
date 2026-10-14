using Romp.BuildingBlocks.Domain;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Tests.Support;

namespace Romp.Modules.Production.Tests.Deliveries;

public sealed class DeliveryNoteTests
{
    [Fact]
    [Trait("Spec", "AC-18")]
    public void Record_BeforeBulkCutting_Refused()
    {
        var run = Runs.WithApprovedSample();

        var error = Assert.Throws<DomainException>(() => Runs.Deliver(run, "DN-1", 50, 25));

        Assert.Contains("record bulk cutting", error.Message);
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public void Record_LineNotOnThePo_Refused()
    {
        var run = Runs.InProduction();

        var error = Assert.Throws<DomainException>(() => run.RecordDeliveryNote(
            "DN-1", new DateOnly(2026, 9, 25), false, [new NoteLineInput(9, 99, 10)], Runs.Terms(), Runs.Person, Runs.Today));

        Assert.Contains("not on PO", error.Message);
    }

    [Theory]
    [Trait("Spec", "AC-18")]
    [InlineData(0)]
    [InlineData(-5)]
    public void Record_NonPositiveQuantity_Refused(int qty)
    {
        var run = Runs.InProduction();

        Assert.Throws<DomainException>(() => run.RecordDeliveryNote(
            "DN-1", new DateOnly(2026, 9, 25), false, [new NoteLineInput(1, 10, qty)], Runs.Terms(), Runs.Person, Runs.Today));
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public void Record_DuplicateSizeColourOnOneNote_Refused()
    {
        var run = Runs.InProduction();

        Assert.Throws<DomainException>(() => run.RecordDeliveryNote(
            "DN-1", new DateOnly(2026, 9, 25), false, [new NoteLineInput(1, 10, 5), new NoteLineInput(1, 10, 6)], Runs.Terms(), Runs.Person, Runs.Today));
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public void Record_DispatchDateBeforeSendOrInTheFuture_Refused()
    {
        var run = Runs.InProduction();

        Assert.Throws<DomainException>(() => Runs.Deliver(run, "DN-1", 50, 25, dispatch: Runs.Sent.AddDays(-1)));
        Assert.Throws<DomainException>(() => Runs.Deliver(run, "DN-2", 50, 25, dispatch: Runs.Today.AddDays(1)));
    }

    [Fact]
    [Trait("Spec", "AC-19")]
    public void Record_SeveralNotes_EachStoredSeparately_AndShippedToDateSumsThem()
    {
        var run = Runs.InProduction();

        Runs.Deliver(run, "DN-1", 40, 20);
        Runs.Deliver(run, "DN-2", 35, 10);

        Assert.Equal(2, run.DeliveryNotes.Count);
        var shipped = run.ShippedToDate();
        Assert.Equal(75, shipped[(1, 10)]);
        Assert.Equal(30, shipped[(2, 10)]);
    }

    [Theory]
    [Trait("Spec", "AC-20")]
    [InlineData("DN-1")]
    [InlineData("dn-1")]
    [InlineData("  DN-1 ")]
    public void Record_DuplicateDnNo_Refused(string second)
    {
        var run = Runs.InProduction();
        Runs.Deliver(run, "DN-1", 40, 20);

        var error = Assert.Throws<DomainException>(() => Runs.Deliver(run, second, 10, 5));

        Assert.Contains("already been recorded", error.Message);
        Assert.Single(run.DeliveryNotes);
    }

    [Fact]
    [Trait("Spec", "AC-21")]
    [Trait("Spec", "D4")]
    public void Record_OverTolerance_RecordedAndFlagged_NeverRefused()
    {
        var run = Runs.InProduction();

        var note = Runs.Deliver(run, "DN-1", 106, 50); // size 1: ordered 100, allowed 105

        Assert.Equal(DeliveryQuantityStatus.Over, note.QuantityStatusId);
        Assert.Equal(1, note.OverQty);
        Assert.Single(run.DeliveryNotes);
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Record_AfterExpectedDate_StoredAsLate_AndTheRevisionUsedIsStored()
    {
        var run = Runs.InProduction();
        var terms = Runs.Terms(revision: 2, expected: new DateOnly(2026, 9, 20), latest: new DateOnly(2026, 10, 25));

        var note = Runs.Deliver(run, "DN-1", 100, 50, dispatch: new DateOnly(2026, 9, 25), terms: terms);

        Assert.Equal(DeliveryTiming.Late, note.TimingId);
        Assert.Equal(5, note.DaysLate);
        Assert.Equal(2, note.RevisionNumber);
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Record_AfterLatestAcceptableDate_StoredAsBeyondAcceptable()
    {
        var run = Runs.InProduction();
        var terms = Runs.Terms(expected: new DateOnly(2026, 9, 10), latest: new DateOnly(2026, 9, 20));

        var note = Runs.Deliver(run, "DN-1", 100, 50, dispatch: new DateOnly(2026, 9, 25), terms: terms);

        Assert.Equal(DeliveryTiming.BeyondAcceptable, note.TimingId);
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public void Record_ReachesTheOrder_RaisesDeliveredOutcome_ElsePartial()
    {
        var run = Runs.InProduction();
        run.ClearDomainEvents();

        Runs.Deliver(run, "DN-1", 40, 20);
        Assert.Equal("PartiallyDelivered", Assert.IsType<DeliveryNoteRecordedEvent>(Assert.Single(run.DomainEvents)).PoOutcome);

        run.ClearDomainEvents();
        Runs.Deliver(run, "DN-2", 55, 28); // totals 95 and 48: both at the 5% under-tolerance minimum
        Assert.Equal("Delivered", Assert.IsType<DeliveryNoteRecordedEvent>(Assert.Single(run.DomainEvents)).PoOutcome);
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    [Trait("Spec", "D3")]
    public void Record_FinalShipmentShort_DeliveredButMarkedShort()
    {
        var run = Runs.InProduction();
        Runs.Deliver(run, "DN-1", 60, 30);
        run.ClearDomainEvents();

        var note = Runs.Deliver(run, "DN-2", 20, 10, final: true);

        Assert.Equal(DeliveryQuantityStatus.Shortage, note.QuantityStatusId);
        Assert.Equal(15 + 8, note.ShortQty); // size 1: min 95 vs 80; size 2: min 48 vs 40
        var recorded = Assert.IsType<DeliveryNoteRecordedEvent>(Assert.Single(run.DomainEvents));
        Assert.Equal("Delivered", recorded.PoOutcome);
        Assert.True(recorded.IsFinalShipment);
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public void Record_ShortWithoutFinalMarker_NeverDelivered()
    {
        var run = Runs.InProduction();
        run.ClearDomainEvents();

        Runs.Deliver(run, "DN-1", 60, 30, final: false);

        Assert.Equal("PartiallyDelivered", Assert.IsType<DeliveryNoteRecordedEvent>(Assert.Single(run.DomainEvents)).PoOutcome);
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public void Record_RaisesEventWithFactsAndNoInternalDetail()
    {
        var run = Runs.InProduction();
        run.ClearDomainEvents();

        Runs.Deliver(run, "DN-1", 106, 50);

        var recorded = Assert.IsType<DeliveryNoteRecordedEvent>(Assert.Single(run.DomainEvents));
        Assert.Equal("DN-1", recorded.VendorDnNo);
        Assert.Equal("Over", recorded.QuantityCode);
        Assert.Equal(1, recorded.OverQty);
        Assert.Equal(2, recorded.Lines.Count);

        var json = System.Text.Json.JsonSerializer.Serialize(recorded, recorded.GetType());
        Assert.DoesNotContain(Runs.Person, json);
    }

    [Fact]
    [Trait("Spec", "AC-37")]
    [Trait("Spec", "D14")]
    public void Record_RompFabricDispatchedAfterCutting_MarksRompFabricDelay()
    {
        var run = Runs.WithApprovedSample(FabricResponsibility.RompSupplied);
        Runs.Record(run, MilestoneType.BulkCuttingStarted, new DateOnly(2026, 9, 10)); // no dispatch yet
        Runs.Record(run, MilestoneType.FabricDispatchedByRomp, new DateOnly(2026, 9, 14));

        var note = Runs.Deliver(run, "DN-1", 100, 50);

        Assert.True(note.IsRompFabricDelay);
        Assert.Equal(new DateOnly(2026, 9, 14), note.FabricDispatchDate);
    }

    [Fact]
    [Trait("Spec", "AC-37")]
    public void Record_RompFabricNeverDispatched_MarksRompFabricDelay()
    {
        var run = Runs.WithApprovedSample(FabricResponsibility.RompSupplied);
        Runs.Record(run, MilestoneType.BulkCuttingStarted, new DateOnly(2026, 9, 10));

        var note = Runs.Deliver(run, "DN-1", 100, 50);

        Assert.True(note.IsRompFabricDelay);
        Assert.Null(note.FabricDispatchDate);
    }

    [Fact]
    [Trait("Spec", "AC-37")]
    public void Record_RompFabricDispatchedBeforeCutting_NoDelay_AndVendorSuppliedNeverDelayed()
    {
        var romp = Runs.InProduction(FabricResponsibility.RompSupplied); // dispatch 8 Sep, cutting 10 Sep
        Assert.False(Runs.Deliver(romp, "DN-1", 100, 50).IsRompFabricDelay);

        var vendor = Runs.InProduction(FabricResponsibility.VendorSupplied);
        var note = Runs.Deliver(vendor, "DN-1", 100, 50);
        Assert.False(note.IsRompFabricDelay);
        Assert.Null(note.FabricDispatchDate);
    }

    [Fact]
    [Trait("Spec", "AC-23")]
    public void Record_UsesTheTermsHandedInForTheDispatchDate()
    {
        // The command layer hands the domain the revision in force on the dispatch date; the domain judges only against it.
        var run = Runs.InProduction();
        var amendedLater = Runs.Terms(revision: 1, expected: new DateOnly(2026, 10, 30), latest: new DateOnly(2026, 11, 10));
        var original = Runs.Terms(revision: 0, expected: new DateOnly(2026, 9, 20), latest: new DateOnly(2026, 9, 24));

        var note = Runs.Deliver(run, "DN-1", 100, 50, dispatch: new DateOnly(2026, 9, 25), terms: original);
        var second = Runs.Deliver(run, "DN-2", 1, 1, dispatch: new DateOnly(2026, 9, 26), terms: amendedLater);

        Assert.Equal(DeliveryTiming.BeyondAcceptable, note.TimingId);
        Assert.Equal(DeliveryTiming.OnTime, second.TimingId);
    }
}
