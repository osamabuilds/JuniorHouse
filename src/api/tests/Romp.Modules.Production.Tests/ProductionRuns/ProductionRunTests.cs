using Romp.BuildingBlocks.Domain;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.ProductionRuns;
using Romp.Modules.Production.Tests.Support;

namespace Romp.Modules.Production.Tests.ProductionRuns;

public sealed class ProductionRunTests
{
    [Fact]
    [Trait("Spec", "AC-1")]
    public void Open_ForAcknowledgedPo_CreatesRunInOpenState()
    {
        var run = Runs.New();

        Assert.True(run.IsOpen);
        Assert.Equal(RunStatus.Open, run.StatusId);
        Assert.Equal(Runs.Sent, run.PoSentDate);
        Assert.Empty(run.Milestones);
        Assert.IsType<ProductionRunOpenedEvent>(Assert.Single(run.DomainEvents));
    }

    [Fact]
    [Trait("Spec", "AC-29")]
    public void Record_OnCancelledRun_Refused()
    {
        var run = Runs.New();
        run.MarkCancelled();

        var error = Assert.Throws<DomainException>(() => Runs.Record(run, MilestoneType.FabricBooked, new DateOnly(2026, 9, 3)));

        Assert.Contains("cancelled", error.Message);
        Assert.Throws<DomainException>(() => run.SubmitPpSample(new DateOnly(2026, 9, 5), Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays));
    }

    [Fact]
    [Trait("Spec", "AC-29")]
    public void DeliveryNote_OnCancelledRun_Refused()
    {
        var run = Runs.InProduction();
        run.MarkCancelled();

        Assert.Throws<DomainException>(() => Runs.Deliver(run, "DN-1", 50, 25));
    }

    [Fact]
    [Trait("Spec", "AC-29")]
    public void MarkCancelled_IsIdempotent_AndRaisesOneEvent()
    {
        var run = Runs.New();
        run.ClearDomainEvents();

        Assert.True(run.MarkCancelled());
        Assert.False(run.MarkCancelled());

        Assert.IsType<ProductionRunCancelledEvent>(Assert.Single(run.DomainEvents));
    }

    [Fact]
    [Trait("Spec", "AC-25")]
    public void MarkClosed_ClosesTheRun_Idempotently_AndIsNotUndoneByCancel()
    {
        var run = Runs.New();

        Assert.True(run.MarkClosed());
        Assert.False(run.MarkClosed());
        Assert.False(run.MarkCancelled());
        Assert.Equal(RunStatus.Closed, run.StatusId);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public void ConstructionRevisionAfterApproval_SetsResampleRecommended()
    {
        var run = Runs.WithApprovedSample();

        run.ApplyRevisionInForce(new DateOnly(2026, 11, 1), isConstructionChange: true);

        Assert.True(run.IsResampleRecommended);
        Assert.True(run.IsApprovalSuperseded);
        Assert.Equal(new DateOnly(2026, 11, 1), run.LatestAcceptableDate);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public void CommercialRevision_OnlyRefreshesDate_AndAllowsButDoesNotRecommendANewRound()
    {
        var run = Runs.WithApprovedSample();

        run.ApplyRevisionInForce(new DateOnly(2026, 11, 1), isConstructionChange: false);

        Assert.False(run.IsResampleRecommended);
        Assert.True(run.IsApprovalSuperseded);
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public void Revision_BeforeAnyApproval_OnlyRefreshesTheDate()
    {
        var run = Runs.New();

        run.ApplyRevisionInForce(new DateOnly(2026, 11, 1), isConstructionChange: true);

        Assert.False(run.IsResampleRecommended);
        Assert.False(run.IsApprovalSuperseded);
        Assert.Equal(new DateOnly(2026, 11, 1), run.LatestAcceptableDate);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public void NewRound_ClearsRecommendation()
    {
        var run = Runs.WithApprovedSample();
        run.ApplyRevisionInForce(new DateOnly(2026, 11, 1), isConstructionChange: true);

        run.SubmitPpSample(new DateOnly(2026, 9, 8), Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays);

        Assert.False(run.IsResampleRecommended);
        Assert.False(run.IsApprovalSuperseded);
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public void Set_KeepsHistory_WithOldAndNewValues()
    {
        var run = Runs.InProduction();

        run.SetExpectedCompletion(new DateOnly(2026, 10, 10), "initial estimate", Runs.Person);
        run.SetExpectedCompletion(new DateOnly(2026, 10, 20), "vendor short of trims", Runs.Person);

        Assert.Equal(new DateOnly(2026, 10, 20), run.ExpectedCompletionDate);
        Assert.Collection(
            run.ExpectedHistory,
            first =>
            {
                Assert.Null(first.OldDate);
                Assert.Equal(new DateOnly(2026, 10, 10), first.NewDate);
            },
            second =>
            {
                Assert.Equal(new DateOnly(2026, 10, 10), second.OldDate);
                Assert.Equal(new DateOnly(2026, 10, 20), second.NewDate);
                Assert.Equal("vendor short of trims", second.Reason);
                Assert.Equal(Runs.Person, second.ActingPerson);
            });
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public void Set_BeforeCuttingStarted_Refused()
    {
        var run = Runs.WithApprovedSample();

        Assert.Throws<DomainException>(() => run.SetExpectedCompletion(new DateOnly(2026, 10, 10), null, Runs.Person));
    }

    [Theory]
    [Trait("Spec", "AC-16")]
    [InlineData("2026-10-17", false)] // latest acceptable 25 Oct minus 7 days = 18 Oct: on the line is not at risk
    [InlineData("2026-10-18", false)]
    [InlineData("2026-10-19", true)]
    [InlineData("2026-10-30", true)]
    public void AtRisk_WithinBuffer_True(string expected, bool atRisk)
    {
        var run = Runs.InProduction();
        run.SetExpectedCompletion(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), null, Runs.Person);

        Assert.Equal(atRisk, run.IsAtRisk(bufferDays: 7));
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public void AtRisk_WithoutAnExpectedDateOrLatestAcceptableDate_False()
    {
        Assert.False(Runs.InProduction().IsAtRisk(7));

        var noLatest = new ProductionRun(1, "PO-2026-00001", 1, Runs.Sent, latestAcceptableDate: null);
        Assert.False(noLatest.IsAtRisk(7));
    }
}
