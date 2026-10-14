using Romp.BuildingBlocks.Domain;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.Samples;
using Romp.Modules.Production.Tests.Support;

namespace Romp.Modules.Production.Tests.Samples;

public sealed class PpSampleRoundTests
{
    private static readonly DateOnly SampleDate = new(2026, 9, 5);

    [Fact]
    [Trait("Spec", "AC-11")]
    public void Submit_FirstRound_IsRoundOneAndPending_AndRecordsAMilestone()
    {
        var run = Runs.New();

        var round = run.SubmitPpSample(SampleDate, Runs.Reporter, Runs.Person, "first sample", Runs.Today, Runs.LateDays);

        Assert.Equal(1, round.RoundNo);
        Assert.True(round.IsPending);
        Assert.Contains(run.Milestones, m => m.TypeId == MilestoneType.PpSampleSubmitted);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public void Submit_WhilePending_Refused()
    {
        var run = Runs.New();
        run.SubmitPpSample(SampleDate, Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays);

        var error = Assert.Throws<DomainException>(() => run.SubmitPpSample(SampleDate, Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays));

        Assert.Contains("already waiting", error.Message);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public void Reject_WithoutReason_Refused()
    {
        var run = Runs.New();
        run.SubmitPpSample(SampleDate, Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays);

        Assert.Throws<DomainException>(() => run.DecidePpSample(1, approve: false, Runs.Person, "  ", Runs.Today, DateTimeOffset.UtcNow));
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    [Trait("Spec", "D6")]
    public void Reject_ThenSubmit_OpensRoundTwo_WithNoLimitOnRounds()
    {
        var run = Runs.New();

        for (var round = 1; round <= 6; round++)
        {
            var submitted = run.SubmitPpSample(SampleDate, Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays);
            Assert.Equal(round, submitted.RoundNo);
            run.DecidePpSample(round, approve: false, Runs.Person, $"fit problem {round}", Runs.Today, DateTimeOffset.UtcNow);
        }

        Assert.Equal(6, run.PpRounds.Count);
        Assert.All(run.PpRounds, r => Assert.Equal(PpDecision.Rejected, r.DecisionId));
        Assert.False(run.HasApprovedPpSample);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    [Trait("Spec", "AC-35")]
    public void Approve_RecordsApproverAndTime_AndAddsAMilestone()
    {
        var run = Runs.New();
        run.SubmitPpSample(SampleDate, Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays);
        var now = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        var round = run.DecidePpSample(1, approve: true, "  Osama Shafique ", null, Runs.Today, now);

        Assert.Equal(PpDecision.Approved, round.DecisionId);
        Assert.Equal("Osama Shafique", round.DecidedBy);
        Assert.Equal(now, round.DecidedAt);
        Assert.True(run.HasApprovedPpSample);
        Assert.Contains(run.Milestones, m => m.TypeId == MilestoneType.PpSampleApproved);
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void Decide_AnAlreadyDecidedRound_Refused()
    {
        var run = Runs.WithApprovedSample();

        Assert.Throws<DomainException>(() => run.DecidePpSample(1, approve: false, Runs.Person, "changed my mind", Runs.Today, DateTimeOffset.UtcNow));
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public void Decide_UnknownRound_NotFound()
    {
        var run = Runs.New();

        Assert.Throws<KeyNotFoundException>(() => run.DecidePpSample(9, approve: true, Runs.Person, null, Runs.Today, DateTimeOffset.UtcNow));
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    public void SubmitAfterApproval_WithoutAmendment_Refused()
    {
        var run = Runs.WithApprovedSample();

        var error = Assert.Throws<DomainException>(() => run.SubmitPpSample(SampleDate, Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays));

        Assert.Contains("already approved", error.Message);
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    [Trait("Spec", "AC-27")]
    public void SubmitAfterApproval_OnceAnAmendmentSupersededIt_Allowed()
    {
        var run = Runs.WithApprovedSample();
        run.ApplyRevisionInForce(new DateOnly(2026, 10, 25), isConstructionChange: false);

        var round = run.SubmitPpSample(SampleDate, Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays);

        Assert.Equal(2, round.RoundNo);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public void Submit_DateOutsideTheAllowedRange_Refused()
    {
        var run = Runs.New();

        Assert.Throws<DomainException>(() => run.SubmitPpSample(Runs.Sent.AddDays(-2), Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays));
        Assert.Throws<DomainException>(() => run.SubmitPpSample(Runs.Today.AddDays(1), Runs.Reporter, Runs.Person, null, Runs.Today, Runs.LateDays));
    }
}
