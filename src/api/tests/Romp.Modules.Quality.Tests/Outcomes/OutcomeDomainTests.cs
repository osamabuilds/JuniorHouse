using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;
using Romp.Modules.Quality.Domain.Outcomes;
using Romp.Modules.Quality.Domain.SafetyChecks;

namespace Romp.Modules.Quality.Tests.Outcomes;

public sealed class ReleasePolicyTests
{
    [Theory]
    [Trait("Spec", "AC-24")]
    [InlineData(QcOutcome.Accepted, true)]
    [InlineData(QcOutcome.Concession, true)]
    [InlineData(QcOutcome.Rejected, false)]
    [InlineData(QcOutcome.Rework, false)]
    public void IsReleasable_Table(short outcome, bool expected)
    {
        Assert.Equal(expected, ReleasePolicy.IsReleasable(outcome));
    }

    [Fact]
    [Trait("Spec", "AC-24")]
    public void IsReleasable_NoOutcomeYet_NeverReleasable()
    {
        Assert.False(ReleasePolicy.IsReleasable(null));
        Assert.Equal([QcOutcome.Accepted, QcOutcome.Concession], ReleasePolicy.ReleasableOutcomes.Order());
    }
}

public sealed class BatchSplitPlannerTests
{
    private static readonly (short SizeId, short ColourId, int Qty)[] Batch = [(1, 10, 600), (2, 10, 400)];

    private static DecisionPart Part(IReadOnlyList<PartLine> lines) => new(QcOutcome.Accepted, null, lines);

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Validate_WholeBatchInOnePart_Accepted()
    {
        BatchSplitPlanner.Validate(Batch, [Part([new(1, 10, 600), new(2, 10, 400)])]);
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Validate_TwoPartsThatAddUp_Accepted()
    {
        BatchSplitPlanner.Validate(Batch, [Part([new(1, 10, 600), new(2, 10, 100)]), Part([new(2, 10, 300)])]);
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Validate_OneUnitShort_Refused()
    {
        var error = Assert.Throws<DomainException>(() => BatchSplitPlanner.Validate(Batch, [Part([new(1, 10, 599), new(2, 10, 400)])]));

        Assert.Contains("only 599 of 600", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Validate_OneUnitOver_Refused()
    {
        var error = Assert.Throws<DomainException>(() => BatchSplitPlanner.Validate(Batch, [Part([new(1, 10, 601), new(2, 10, 400)])]));

        Assert.Contains("601 assigned", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Validate_UnitCountedInTwoParts_Refused()
    {
        Assert.Throws<DomainException>(() => BatchSplitPlanner.Validate(Batch, [Part([new(1, 10, 600), new(2, 10, 400)]), Part([new(2, 10, 1)])]));
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Validate_WrongSizeOrColour_Refused()
    {
        Assert.Throws<DomainException>(() => BatchSplitPlanner.Validate(Batch, [Part([new(1, 10, 600), new(2, 10, 400), new(9, 99, 1)])]));
    }

    [Theory]
    [Trait("Spec", "AC-22")]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_ZeroOrNegativeQuantity_Refused(int qty)
    {
        Assert.Throws<DomainException>(() => BatchSplitPlanner.Validate(Batch, [Part([new(1, 10, 600), new(2, 10, 400), new(1, 10, qty)])]));
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Validate_EmptyPartsOrEmptyPart_Refused()
    {
        Assert.Throws<DomainException>(() => BatchSplitPlanner.Validate(Batch, []));
        Assert.Throws<DomainException>(() => BatchSplitPlanner.Validate(Batch, [Part([new(1, 10, 600), new(2, 10, 400)]), Part([])]));
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    public void Validate_SameLineTwiceInOnePart_Refused()
    {
        Assert.Throws<DomainException>(() => BatchSplitPlanner.Validate(Batch, [Part([new(1, 10, 300), new(1, 10, 300), new(2, 10, 400)])]));
    }
}

public sealed class BatchDecisionTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly AqlPlanTable Table = new(AqlSeedData.SampleRows(), AqlSeedData.LimitRows());

    private static QcBatch InspectedBatch(bool safetyRecorded = true)
    {
        var batch = QcBatch.OpenFor(1, 7, "PO-2026-00007", [((short)1, (short)10, 600), ((short)2, (short)10, 400)])!;
        var plan = AqlCalculator.Plan(Table, batch.LotQty, InspectionLevel.II, AqlLevel.Aql250, 50);
        var inspection = batch.RecordInspection(
            new InspectionInput(null, Today, "Ali Raza", null, InspectionLevel.II, AqlLevel.Aql250,
                CheckType.All.Select(t => new CheckInput(t, CheckResult.Pass)).ToList(), []),
            plan, 0, Today);
        if (safetyRecorded)
        {
            batch.RecordSafetyChecks(inspection.Id, [new(SafetyType.ChokingHazardTrims, CheckResult.Pass, null, []), new(SafetyType.DrawstringNeckline, CheckResult.Pass, null, [])], "Ali Raza");
        }

        return batch;
    }

    private static DecisionPart All(short outcome, string? reason = null) => new(outcome, reason, [new(1, 10, 600), new(2, 10, 400)]);

    [Fact]
    [Trait("Spec", "AC-19")]
    public void Decide_NoInspection_Refused()
    {
        var batch = QcBatch.OpenFor(1, 7, "PO-2026-00007", [((short)1, (short)10, 100)])!;

        var error = Assert.Throws<DomainException>(() => batch.Decide([new(QcOutcome.Accepted, null, [new(1, 10, 100)])], "Ali Raza", Now));

        Assert.Contains("Record an inspection", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "AC-20")]
    [InlineData(QcOutcome.Concession)]
    [InlineData(QcOutcome.Rejected)]
    [InlineData(QcOutcome.Rework)]
    public void Decide_WithoutReason_RefusedExceptForAccepted(short outcome)
    {
        Assert.Throws<DomainException>(() => InspectedBatch().Decide([All(outcome)], "Ali Raza", Now));
        Assert.Throws<DomainException>(() => InspectedBatch().Decide([All(outcome, "  ")], "Ali Raza", Now));
    }

    [Fact]
    [Trait("Spec", "AC-20")]
    public void Decide_AcceptedNeedsNoReason_AndUnknownOutcomeRefused()
    {
        InspectedBatch().Decide([All(QcOutcome.Accepted)], "Ali Raza", Now);
        Assert.Throws<DomainException>(() => InspectedBatch().Decide([All(9)], "Ali Raza", Now));
    }

    [Fact]
    [Trait("Spec", "AC-25")]
    public void Decide_Twice_Refused()
    {
        var batch = InspectedBatch();
        batch.Decide([All(QcOutcome.Accepted)], "Ali Raza", Now);

        var error = Assert.Throws<DomainException>(() => batch.Decide([All(QcOutcome.Rejected, "changed my mind")], "Ali Raza", Now));

        Assert.Contains("already been decided", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public void Decide_SafetyGateRefuses_Refused()
    {
        var batch = InspectedBatch(safetyRecorded: false);

        Assert.Throws<DomainException>(() => batch.Decide([All(QcOutcome.Accepted)], "Ali Raza", Now));
        Assert.Empty(batch.SubBatches); // nothing was created by the refused decision
    }

    [Fact]
    [Trait("Spec", "AC-31")]
    public void Decide_RejectNeedsNoSafetyChecks()
    {
        InspectedBatch(safetyRecorded: false).Decide([All(QcOutcome.Rejected, "wrong fabric")], "Ali Raza", Now);
    }

    [Fact]
    [Trait("Spec", "AC-32")]
    public void Decide_FailedLine_OnlyRejectOrReworkForThatLine()
    {
        var batch = InspectedBatch(safetyRecorded: false);
        var inspection = batch.Inspections.Single();
        batch.RecordSafetyChecks(
            inspection.Id,
            [new(SafetyType.ChokingHazardTrims, CheckResult.Fail, null, [new LineRef(2, 10)]), new(SafetyType.DrawstringNeckline, CheckResult.Pass, null, [])],
            "Ali Raza");

        Assert.Throws<DomainException>(() => batch.Decide([All(QcOutcome.Accepted)], "Ali Raza", Now));
        var parts = new List<DecisionPart>
        {
            new(QcOutcome.Accepted, null, [new(1, 10, 600)]),
            new(QcOutcome.Rejected, "buttons not secure", [new(2, 10, 400)]),
        };
        var created = batch.Decide(parts, "Ali Raza", Now);

        Assert.Equal([QcOutcome.Accepted, QcOutcome.Rejected], created.Select(s => s.OutcomeId!.Value).ToArray());
    }

    [Fact]
    [Trait("Spec", "AC-22")]
    [Trait("Spec", "AC-23")]
    public void Decide_Split_PassingReleasedRejectedNot_AndReworkDoesNotHoldAccepted()
    {
        var batch = InspectedBatch();
        var parts = new List<DecisionPart>
        {
            new(QcOutcome.Accepted, null, [new(1, 10, 600), new(2, 10, 250)]),
            new(QcOutcome.Rejected, "torn seams", [new(2, 10, 100)]),
            new(QcOutcome.Rework, "re-sew hems", [new(2, 10, 50)]),
        };

        var created = batch.Decide(parts, "Ali Raza", Now);

        Assert.Equal(3, created.Count);
        Assert.Equal(BatchStatus.Decided, created[0].StatusId); // released now, not waiting for the rework
        Assert.Equal(BatchStatus.Decided, created[1].StatusId);
        Assert.Equal(BatchStatus.AwaitingRework, created[2].StatusId);
        Assert.Equal(BatchStatus.AwaitingRework, batch.StatusId);
        Assert.Equal(1, created[2].ReworkRoundCount);
        Assert.Equal([ReleasePolicy.IsReleasable(QcOutcome.Accepted), false, false], created.Select(s => ReleasePolicy.IsReleasable(s.OutcomeId)).ToArray());
    }

    [Fact]
    [Trait("Spec", "AC-21")]
    public void RaiseOutcomeEvent_Concession_CarriesTheTag()
    {
        var batch = InspectedBatch();
        var created = batch.Decide([All(QcOutcome.Concession, "minor shade variation, sold as-is")], "Ali Raza", Now);

        batch.RaiseOutcomeEvent(created[0]);

        var raised = Assert.Single(batch.DomainEvents.OfType<QcBatchAcceptedEvent>());
        Assert.Equal("Concession", raised.OutcomeCode);
        Assert.Equal(2, raised.Lines.Count);
        Assert.DoesNotContain("shade", System.Text.Json.JsonSerializer.Serialize(raised), StringComparison.Ordinal); // the reason never leaves QC (AC-29)
    }

    [Fact]
    [Trait("Spec", "AC-29")]
    public void RaiseOutcomeEvent_EachOutcomeRaisesItsOwnEvent()
    {
        var batch = InspectedBatch();
        var created = batch.Decide(
            [
                new(QcOutcome.Accepted, null, [new(1, 10, 600)]),
                new(QcOutcome.Rejected, "wrong colour", [new(2, 10, 300)]),
                new(QcOutcome.Rework, "fix labels", [new(2, 10, 100)]),
            ], "Ali Raza", Now);

        foreach (var sub in created)
        {
            batch.RaiseOutcomeEvent(sub);
        }

        Assert.Equal(
            ["QcBatchAcceptedEvent", "QcBatchRejectedEvent", "QcBatchReworkRequestedEvent"],
            batch.DomainEvents.Select(e => e.GetType().Name).Where(n => n != "QcInspectionRecordedEvent").ToArray());
    }
}

public sealed class ReworkReturnTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly AqlPlanTable Table = new(AqlSeedData.SampleRows(), AqlSeedData.LimitRows());

    /// <summary>A batch with a rework part of 100 units (size 2 / colour 10) and an accepted rest.</summary>
    private static (QcBatch Batch, QcSubBatch Rework) BatchWithRework()
    {
        var batch = QcBatch.OpenFor(1, 7, "PO-2026-00007", [((short)1, (short)10, 600), ((short)2, (short)10, 400)])!;
        var plan = AqlCalculator.Plan(Table, batch.LotQty, InspectionLevel.II, AqlLevel.Aql250, 50);
        var inspection = batch.RecordInspection(
            new InspectionInput(null, Today, "Ali Raza", null, InspectionLevel.II, AqlLevel.Aql250,
                CheckType.All.Select(t => new CheckInput(t, CheckResult.Pass)).ToList(), []),
            plan, 0, Today);
        batch.RecordSafetyChecks(inspection.Id, [new(SafetyType.ChokingHazardTrims, CheckResult.Pass, null, []), new(SafetyType.DrawstringNeckline, CheckResult.Pass, null, [])], "Ali Raza");
        var created = batch.Decide(
            [new(QcOutcome.Accepted, null, [new(1, 10, 600), new(2, 10, 300)]), new(QcOutcome.Rework, "re-sew hems", [new(2, 10, 100)])], "Ali Raza", Now);
        return (batch, created[1]);
    }

    private static long idSeed;

    private static void SetIds(QcBatch batch)
    {
        // The domain tests run without a database, so give each part the id EF would assign.
        long next = Interlocked.Increment(ref idSeed) * 1000;
        var id = typeof(Romp.BuildingBlocks.Domain.Entity<long>).GetProperty("Id")!;
        foreach (var sub in batch.SubBatches.Where(s => s.Id == 0))
        {
            id.SetValue(sub, next++);
        }

        foreach (var inspection in batch.Inspections.Where(i => i.Id == 0))
        {
            id.SetValue(inspection, next++);
        }
    }

    private static List<CheckInput> Passed() => CheckType.All.Select(t => new CheckInput(t, CheckResult.Pass)).ToList();

    [Fact]
    [Trait("Spec", "AC-26")]
    public void Return_Valid_OpensAPartAwaitingReInspection()
    {
        var (batch, rework) = BatchWithRework();
        SetIds(batch);

        var returned = batch.RecordReworkReturn(rework.Id, Today, "Sara Khan", "collected from vendor", [new(2, 10, 100)], Today);

        Assert.Equal(BatchStatus.Pending, returned.StatusId);
        Assert.Equal(rework.Id, returned.ParentSubBatchId);
        Assert.Equal(1, returned.ReworkRoundCount);
        Assert.Equal(100, Assert.Single(returned.Lines).Qty);
        Assert.Equal(BatchStatus.Decided, rework.StatusId); // everything came back, so the rework part is settled
        Assert.Equal(BatchStatus.AwaitingRework, batch.StatusId); // the returned part still awaits its re-inspection
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public void Return_MoreThanSent_Refused()
    {
        var (batch, rework) = BatchWithRework();
        SetIds(batch);

        var error = Assert.Throws<DomainException>(() => batch.RecordReworkReturn(rework.Id, Today, "Sara Khan", null, [new(2, 10, 101)], Today));

        Assert.Contains("only 100", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-27")]
    public void Return_ShortReturn_ShortfallStaysOutstanding()
    {
        var (batch, rework) = BatchWithRework();
        SetIds(batch);

        batch.RecordReworkReturn(rework.Id, Today, "Sara Khan", null, [new(2, 10, 60)], Today);

        var line = Assert.Single(rework.Lines);
        Assert.Equal((100, 60, 40), (line.Qty, line.ReturnedQty, line.OutstandingQty));
        Assert.Equal(BatchStatus.AwaitingRework, rework.StatusId); // 40 units are still with the vendor
        Assert.Equal(60, batch.SubBatches.Single(s => s.ParentSubBatchId == rework.Id).Lines.Sum(l => l.Qty));

        var second = batch.RecordReworkReturn(rework.Id, Today, "Sara Khan", null, [new(2, 10, 40)], Today);
        Assert.Equal(40, second.Lines.Sum(l => l.Qty));
        Assert.Equal(BatchStatus.Decided, rework.StatusId);
        Assert.Equal(2, rework.Returns.Count);
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public void Return_ForAPartNotSentForRework_Refused()
    {
        var (batch, _) = BatchWithRework();
        SetIds(batch);
        var accepted = batch.SubBatches.First(s => s.OutcomeId == QcOutcome.Accepted);

        Assert.Throws<DomainException>(() => batch.RecordReworkReturn(accepted.Id, Today, "Sara Khan", null, [new(1, 10, 1)], Today));
        Assert.Throws<KeyNotFoundException>(() => batch.RecordReworkReturn(9999, Today, "Sara Khan", null, [new(1, 10, 1)], Today));
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public void Return_InvalidInput_Refused()
    {
        var (batch, rework) = BatchWithRework();
        SetIds(batch);

        Assert.Throws<DomainException>(() => batch.RecordReworkReturn(rework.Id, Today.AddDays(1), "Sara Khan", null, [new(2, 10, 5)], Today));
        Assert.Throws<DomainException>(() => batch.RecordReworkReturn(rework.Id, Today, "Sara Khan", null, [], Today));
        Assert.Throws<DomainException>(() => batch.RecordReworkReturn(rework.Id, Today, "Sara Khan", null, [new(2, 10, 0)], Today));
        Assert.Throws<DomainException>(() => batch.RecordReworkReturn(rework.Id, Today, "Sara Khan", null, [new(1, 10, 5)], Today)); // not sent for rework
        Assert.Throws<DomainException>(() => batch.RecordReworkReturn(rework.Id, Today, "S", null, [new(2, 10, 5)], Today));
    }

    [Fact]
    [Trait("Spec", "AC-34")]
    public void ReturnedPart_MustBeReInspectedAndSafetyCheckedAgain_BeforeAcceptance()
    {
        var (batch, rework) = BatchWithRework();
        SetIds(batch);
        var returned = batch.RecordReworkReturn(rework.Id, Today, "Sara Khan", null, [new(2, 10, 100)], Today);
        SetIds(batch);

        // No re-inspection yet.
        Assert.Throws<DomainException>(() => batch.DecideSubBatch(returned.Id, QcOutcome.Accepted, null, "Ali Raza", Now));

        var plan = AqlCalculator.Plan(Table, batch.ScopeQty(returned.Id), InspectionLevel.II, AqlLevel.Aql250, 50);
        var round = batch.RecordInspection(
            new InspectionInput(returned.Id, Today, "Ali Raza", null, InspectionLevel.II, AqlLevel.Aql250, Passed(), []), plan, 0, Today);
        SetIds(batch);
        Assert.Equal((short)2, round.RoundNo); // the returned quantity's first inspection is round 2
        Assert.Equal(BatchStatus.InInspection, returned.StatusId);

        // Inspected, but the safety checks for this new round are missing.
        Assert.Throws<DomainException>(() => batch.DecideSubBatch(returned.Id, QcOutcome.Accepted, null, "Ali Raza", Now));

        batch.RecordSafetyChecks(round.Id, [new(SafetyType.ChokingHazardTrims, CheckResult.Pass, null, []), new(SafetyType.DrawstringNeckline, CheckResult.Pass, null, [])], "Ali Raza");
        var decided = batch.DecideSubBatch(returned.Id, QcOutcome.Accepted, null, "Ali Raza", Now);

        Assert.Equal(BatchStatus.Decided, decided.StatusId);
        Assert.True(ReleasePolicy.IsReleasable(decided.OutcomeId));
        Assert.Equal(BatchStatus.Decided, batch.StatusId); // nothing left outstanding
    }

    [Fact]
    [Trait("Spec", "AC-26")]
    public void ReturnedPart_ReworkAgain_ThirdRoundAllowed_AndNoLimitOnRounds()
    {
        var (batch, rework) = BatchWithRework();
        SetIds(batch);
        var current = rework;
        for (var round = 1; round <= 4; round++)
        {
            var returned = batch.RecordReworkReturn(current.Id, Today, "Sara Khan", null, [new(2, 10, 100)], Today);
            SetIds(batch);
            var plan = AqlCalculator.Plan(Table, batch.ScopeQty(returned.Id), InspectionLevel.II, AqlLevel.Aql250, 50);
            batch.RecordInspection(new InspectionInput(returned.Id, Today, "Ali Raza", null, InspectionLevel.II, AqlLevel.Aql250, Passed(), []), plan, 0, Today);

            current = batch.DecideSubBatch(returned.Id, QcOutcome.Rework, "still wrong", "Ali Raza", Now);

            Assert.Equal(round + 1, current.ReworkRoundCount);
            Assert.Equal(BatchStatus.AwaitingRework, current.StatusId);
        }
    }

    [Fact]
    [Trait("Spec", "AC-25")]
    public void DecideSubBatch_Twice_Refused_AndStillWithVendorRefused()
    {
        var (batch, rework) = BatchWithRework();
        SetIds(batch);

        Assert.Throws<DomainException>(() => batch.DecideSubBatch(rework.Id, QcOutcome.Accepted, null, "Ali Raza", Now)); // still with the vendor
        var accepted = batch.SubBatches.First(s => s.OutcomeId == QcOutcome.Accepted);
        Assert.Throws<DomainException>(() => batch.DecideSubBatch(accepted.Id, QcOutcome.Rejected, "no", "Ali Raza", Now)); // already decided
    }
}
