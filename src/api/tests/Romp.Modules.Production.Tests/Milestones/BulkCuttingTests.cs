using Romp.BuildingBlocks.Domain;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Tests.Support;

namespace Romp.Modules.Production.Tests.Milestones;

public sealed class BulkCuttingTests
{
    private static readonly DateOnly Cutting = new(2026, 9, 10);

    [Fact]
    [Trait("Spec", "AC-9")]
    public void Record_NoApprovedRound_Refused_NamingTheMissingApproval()
    {
        var run = Runs.New();

        var error = Assert.Throws<DomainException>(() => Runs.Record(run, MilestoneType.BulkCuttingStarted, Cutting));

        Assert.Contains("pre-production sample has been approved", error.Message);
        Assert.False(run.IsCuttingStarted);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public void Record_OnlyARejectedOrPendingRound_StillRefused()
    {
        var run = Runs.New();
        run.SubmitPpSample(new DateOnly(2026, 9, 5), Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays);
        Assert.Throws<DomainException>(() => Runs.Record(run, MilestoneType.BulkCuttingStarted, Cutting));

        run.DecidePpSample(1, approve: false, Runs.Person, "wrong colour", Runs.Today, DateTimeOffset.UtcNow);
        Assert.Throws<DomainException>(() => Runs.Record(run, MilestoneType.BulkCuttingStarted, Cutting));
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    [Trait("Spec", "AC-14")]
    public void Record_AfterApproval_StartsProduction_AndRaisesTheEventForVndr()
    {
        var run = Runs.WithApprovedSample();
        run.ClearDomainEvents();

        Runs.Record(run, MilestoneType.BulkCuttingStarted, Cutting);

        Assert.Equal(Cutting, run.CuttingStartedDate);
        Assert.Contains(run.DomainEvents, e => e is ProductionStartedEvent);
        Assert.Contains(run.DomainEvents, e => e is ProductionMilestoneReachedEvent { MilestoneCode: "BulkCuttingStarted" });
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public void Record_Twice_Refused()
    {
        var run = Runs.InProduction();

        Assert.Throws<DomainException>(() => Runs.Record(run, MilestoneType.BulkCuttingStarted, new DateOnly(2026, 9, 12)));
    }

    [Fact]
    [Trait("Spec", "AC-36")]
    [Trait("Spec", "D14")]
    public void Record_RompSuppliedNoFabricDispatch_RecordedWithLoudFlag_NeverBlocked()
    {
        var run = Runs.WithApprovedSample(FabricResponsibility.RompSupplied);

        var milestone = Runs.Record(run, MilestoneType.BulkCuttingStarted, Cutting);

        Assert.True(milestone.IsFlagged);
        Assert.Contains("before Romp's fabric was dispatched", milestone.FlagReason);
        Assert.True(run.IsCuttingBeforeFabricDispatch);
        Assert.True(run.IsCuttingStarted);
    }

    [Fact]
    [Trait("Spec", "AC-36")]
    public void Record_RompSuppliedFabricDispatchedAfterTheCuttingDate_StillFlagged()
    {
        var run = Runs.WithApprovedSample(FabricResponsibility.RompSupplied);
        Runs.Record(run, MilestoneType.FabricDispatchedByRomp, new DateOnly(2026, 9, 12));

        var milestone = Runs.Record(run, MilestoneType.BulkCuttingStarted, Cutting); // cutting 10th, fabric 12th

        Assert.True(milestone.IsFlagged);
    }

    [Fact]
    [Trait("Spec", "AC-36")]
    public void Record_RompSuppliedFabricDispatchedOnOrBeforeCutting_NotFlagged()
    {
        var run = Runs.WithApprovedSample(FabricResponsibility.RompSupplied);
        Runs.Record(run, MilestoneType.FabricDispatchedByRomp, Cutting);

        var milestone = Runs.Record(run, MilestoneType.BulkCuttingStarted, Cutting);

        Assert.False(milestone.IsFlagged);
        Assert.False(run.IsCuttingBeforeFabricDispatch);
    }

    [Fact]
    [Trait("Spec", "AC-36")]
    public void Record_VendorSupplied_NeverFlaggedForFabricDispatch()
    {
        var run = Runs.WithApprovedSample(FabricResponsibility.VendorSupplied);

        var milestone = Runs.Record(run, MilestoneType.BulkCuttingStarted, Cutting);

        Assert.False(milestone.IsFlagged);
    }

    [Fact]
    [Trait("Spec", "AC-38")]
    [Trait("Spec", "D17")]
    public void Record_ResampleRecommended_RecordedWithLoudFlag_NeverBlocked()
    {
        var run = Runs.WithApprovedSample();
        run.ApplyRevisionInForce(new DateOnly(2026, 10, 25), isConstructionChange: true);
        Assert.True(run.IsResampleRecommended);

        var milestone = Runs.Record(run, MilestoneType.BulkCuttingStarted, Cutting);

        Assert.True(milestone.IsFlagged);
        Assert.Contains("without re-sampling", milestone.FlagReason);
        Assert.True(run.IsCuttingWithoutResample);
    }

    [Fact]
    [Trait("Spec", "AC-38")]
    public void Record_ResampleDoneAfterTheConstructionChange_NotFlagged()
    {
        var run = Runs.WithApprovedSample();
        run.ApplyRevisionInForce(new DateOnly(2026, 10, 25), isConstructionChange: true);
        run.SubmitPpSample(new DateOnly(2026, 9, 8), Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays);
        run.DecidePpSample(2, approve: true, Runs.Person, null, Runs.Today, DateTimeOffset.UtcNow);

        var milestone = Runs.Record(run, MilestoneType.BulkCuttingStarted, Cutting);

        Assert.False(milestone.IsFlagged);
    }
}
