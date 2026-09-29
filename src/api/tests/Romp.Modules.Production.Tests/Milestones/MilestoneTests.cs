using Romp.BuildingBlocks.Domain;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Tests.Support;

namespace Romp.Modules.Production.Tests.Milestones;

public sealed class MilestoneTests
{
    [Fact]
    [Trait("Spec", "AC-4")]
    public void Record_FabricBooked_OnlyWhenVendorSupplied()
    {
        var vendorRun = Runs.New(FabricResponsibility.VendorSupplied);
        Runs.Record(vendorRun, MilestoneType.FabricBooked, new DateOnly(2026, 9, 3));

        var rompRun = Runs.New(FabricResponsibility.RompSupplied);
        var error = Assert.Throws<DomainException>(() => Runs.Record(rompRun, MilestoneType.FabricBooked, new DateOnly(2026, 9, 3)));
        Assert.Contains("vendor supplies the fabric", error.Message);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    [Trait("Spec", "D14")]
    public void Record_FabricDispatchedByRomp_OnlyWhenRompSupplied()
    {
        var rompRun = Runs.New(FabricResponsibility.RompSupplied);
        Runs.Record(rompRun, MilestoneType.FabricDispatchedByRomp, new DateOnly(2026, 9, 3));
        Assert.Equal(new DateOnly(2026, 9, 3), rompRun.FabricDispatchDate);

        var vendorRun = Runs.New(FabricResponsibility.VendorSupplied);
        Assert.Throws<DomainException>(() => Runs.Record(vendorRun, MilestoneType.FabricDispatchedByRomp, new DateOnly(2026, 9, 3)));
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public void Record_NoFabricResponsibilityOnThePo_NeitherFabricMilestoneApplies()
    {
        var run = Runs.New(fabric: null);

        Assert.Throws<DomainException>(() => Runs.Record(run, MilestoneType.FabricBooked, new DateOnly(2026, 9, 3)));
        Assert.Throws<DomainException>(() => Runs.Record(run, MilestoneType.FabricDispatchedByRomp, new DateOnly(2026, 9, 3)));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    [Trait("Spec", "AC-35")]
    public void Record_StoresClaimedDateReporterAndActingPerson()
    {
        var run = Runs.New();

        var milestone = run.RecordMilestone(MilestoneType.FabricBooked, new DateOnly(2026, 9, 3), "  Ali Raza ", "  Osama Shafique ", " fabric is in ", Runs.Today, Runs.LateDays);

        Assert.Equal(new DateOnly(2026, 9, 3), milestone.ClaimedDate);
        Assert.Equal("Ali Raza", milestone.ReportedBy);
        Assert.Equal("Osama Shafique", milestone.ActingPerson);
        Assert.Equal("fabric is in", milestone.Note);
    }

    [Theory]
    [Trait("Spec", "AC-35")]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("   ")]
    public void Record_ActingPersonTooShort_Refused(string person)
    {
        var run = Runs.New();

        Assert.Throws<DomainException>(() => run.RecordMilestone(MilestoneType.FabricBooked, new DateOnly(2026, 9, 3), Runs.Reporter, person, null, Runs.Today, Runs.LateDays));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void Record_DateBeforePoWasSent_RefusedWithTheAllowedRange()
    {
        var run = Runs.New();

        var error = Assert.Throws<DomainException>(() => Runs.Record(run, MilestoneType.FabricBooked, Runs.Sent.AddDays(-1)));

        Assert.Contains("2026-09-01", error.Message);
        Assert.Contains("2026-09-29", error.Message);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void Record_DateInTheFuture_Refused()
    {
        var run = Runs.New();

        Assert.Throws<DomainException>(() => Runs.Record(run, MilestoneType.FabricBooked, Runs.Today.AddDays(1)));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void Record_OnTheSendDateAndOnToday_Allowed()
    {
        var run = Runs.New();

        Runs.Record(run, MilestoneType.FabricBooked, Runs.Sent);
        Runs.Record(run, MilestoneType.FabricBooked, Runs.Today);

        Assert.Equal(2, run.Milestones.Count);
    }

    [Theory]
    [Trait("Spec", "AC-7")]
    [InlineData(0, false)]
    [InlineData(3, false)] // exactly the threshold: not late
    [InlineData(4, true)]  // one day past the threshold
    [InlineData(20, true)]
    public void Record_MoreThanThresholdLater_FlaggedRecordedLate_ButStillSaved(int daysAgo, bool late)
    {
        var run = Runs.New();

        var milestone = Runs.Record(run, MilestoneType.FabricBooked, Runs.Today.AddDays(-daysAgo));

        Assert.Equal(late, milestone.IsRecordedLate);
        Assert.Single(run.Milestones);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    [Trait("Spec", "D8")]
    public void Skip_OptionalMilestones_LaterOnesStillRecordable()
    {
        // No FabricBooked and no FinishedReadyToShip: the sample, cutting and a delivery all still work.
        var run = Runs.InProduction();

        Runs.Deliver(run, "DN-1", 100, 50, final: false);

        Assert.DoesNotContain(run.Milestones, m => m.TypeId is MilestoneType.FabricBooked or MilestoneType.FinishedReadyToShip);
        Assert.Single(run.DeliveryNotes);
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    [Trait("Spec", "D8")]
    public void Record_FinishedReadyToShip_StoresPositiveLines_AndGatesNothing()
    {
        var run = Runs.InProduction();

        var milestone = run.RecordMilestone(
            MilestoneType.FinishedReadyToShip, new DateOnly(2026, 9, 20), Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays,
            [((short)1, (short)10, 80), ((short)2, (short)10, 40)]);

        Assert.Equal(2, milestone.FinishedLines.Count);
        Assert.Equal(120, milestone.FinishedLines.Sum(l => l.Qty));
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    public void Record_FinishedReadyToShip_ZeroOrNegativeQuantity_Refused()
    {
        var run = Runs.InProduction();

        Assert.Throws<DomainException>(() => run.RecordMilestone(
            MilestoneType.FinishedReadyToShip, new DateOnly(2026, 9, 20), Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays,
            [((short)1, (short)10, 0)]));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public void Record_PlainMilestoneWithAPpType_Refused()
    {
        var run = Runs.New();

        Assert.Throws<DomainException>(() => Runs.Record(run, MilestoneType.PpSampleApproved, new DateOnly(2026, 9, 3)));
    }

    [Fact]
    [Trait("Spec", "AC-30")]
    public void Record_RaisesMilestoneReachedWithNoInternalDetail()
    {
        var run = Runs.New();
        run.ClearDomainEvents();

        run.RecordMilestone(MilestoneType.FabricBooked, new DateOnly(2026, 9, 3), "Ali Raza", Runs.Person, "private note", Runs.Today, Runs.LateDays);

        var reached = Assert.IsType<ProductionMilestoneReachedEvent>(Assert.Single(run.DomainEvents));
        Assert.Equal("FabricBooked", reached.MilestoneCode);
        Assert.Equal(new DateOnly(2026, 9, 3), reached.ClaimedDate);

        // The payload never carries the note, the reporter or the acting person (AC-30).
        var json = System.Text.Json.JsonSerializer.Serialize(reached, reached.GetType());
        Assert.DoesNotContain("private note", json);
        Assert.DoesNotContain("Ali Raza", json);
        Assert.DoesNotContain(Runs.Person, json);
    }
}
