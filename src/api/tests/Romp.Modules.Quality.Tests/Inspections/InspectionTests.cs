using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Domain.Batches;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;

namespace Romp.Modules.Quality.Tests.Inspections;

public sealed class InspectionScorerTests
{
    private static DefectInput Defect(short severity, int qty) => new(DefectType.VisibleDefect, severity, qty, null, null, "note");

    [Theory]
    [Trait("Spec", "AC-18")]
    [InlineData(0, 5, 6, InspectionResult.Pass)] // nothing found
    [InlineData(5, 5, 6, InspectionResult.Pass)] // exactly the accept number
    [InlineData(6, 5, 6, InspectionResult.Fail)] // exactly the reject number
    [InlineData(9, 5, 6, InspectionResult.Fail)]
    public void Score_MajorAndMinorTogether_AgainstAcceptAndReject(int minorQty, int accept, int reject, short expected)
    {
        var score = InspectionScorer.Score(minorQty == 0 ? [] : [Defect(DefectSeverity.Minor, minorQty)], 80, accept, reject, criticalTolerance: 0);

        Assert.Equal(expected, score.ResultId);
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public void Score_MajorAndMinorAreAddedBeforeComparing()
    {
        var score = InspectionScorer.Score([Defect(DefectSeverity.Major, 3), Defect(DefectSeverity.Minor, 3)], 80, 5, 6, 0);

        Assert.Equal(InspectionResult.Fail, score.ResultId);
        Assert.Equal((0, 3, 3), (score.CriticalCount, score.MajorCount, score.MinorCount));
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public void Score_AnyCritical_Fails_UnlessToleranceAllowsIt()
    {
        Assert.Equal(InspectionResult.Fail, InspectionScorer.Score([Defect(DefectSeverity.Critical, 1)], 80, 5, 6, criticalTolerance: 0).ResultId);
        Assert.Equal(InspectionResult.Pass, InspectionScorer.Score([Defect(DefectSeverity.Critical, 1)], 80, 5, 6, criticalTolerance: 1).ResultId);
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public void Score_DefectiveUnits_NeverExceedTheSample()
    {
        var score = InspectionScorer.Score([Defect(DefectSeverity.Minor, 30), Defect(DefectSeverity.Major, 30)], 40, 5, 6, 0);

        Assert.Equal(40, score.DefectiveQty);
    }
}

public sealed class InspectionTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);
    private static readonly AqlPlanTable Table = new(AqlSeedData.SampleRows(), AqlSeedData.LimitRows());

    private static QcBatch Batch(int line1 = 600, int line2 = 400) =>
        QcBatch.OpenFor(grnId: 1, poId: 7, poNo: "PO-2026-00007", [((short)1, (short)10, line1), ((short)2, (short)10, line2)])!;

    private static AqlPlan PlanFor(QcBatch batch) => AqlCalculator.Plan(Table, batch.LotQty, InspectionLevel.II, AqlLevel.Aql250, 50);

    private static List<CheckInput> Checks(params short[] failed) =>
        CheckType.All.Select(t => new CheckInput(t, failed.Contains(t) ? CheckResult.Fail : CheckResult.Pass)).ToList();

    private static DefectInput Defect(short severity, int qty, short type = DefectType.VisibleDefect, string note = "loose thread") =>
        new(type, severity, qty, null, null, note);

    private static InspectionInput Input(
        IReadOnlyList<CheckInput>? checks = null, IReadOnlyList<DefectInput>? defects = null, int? sample = null,
        string person = "Ali Raza", DateOnly? date = null) =>
        new(null, date ?? Today, person, sample, InspectionLevel.II, AqlLevel.Aql250, checks ?? Checks(), defects ?? []);

    private static Inspection Record(QcBatch batch, InspectionInput input) => batch.RecordInspection(input, PlanFor(batch), 0, Today);

    [Fact]
    [Trait("Spec", "AC-16")]
    public void Record_Valid_StoresSampleChecklistPersonAndDate()
    {
        var batch = Batch();

        var inspection = Record(batch, Input());

        Assert.Equal(1000, inspection.LotQty);
        Assert.Equal(80, inspection.SampleQty);
        Assert.Equal((5, 6), (inspection.AcceptNo, inspection.RejectNo));
        Assert.Equal(6, inspection.Checks.Count);
        Assert.Equal("Ali Raza", inspection.ActingPerson);
        Assert.Equal(Today, inspection.InspectionDate);
        Assert.Equal((short)1, inspection.RoundNo);
        Assert.Equal(BatchStatus.InInspection, batch.StatusId);
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public void Record_MissingChecklistItem_Refused()
    {
        var error = Assert.Throws<DomainException>(() => Record(Batch(), Input(checks: Checks().Take(5).ToList())));

        Assert.Contains("every checklist item", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public void Record_NotApplicableOnTheChecklist_Refused()
    {
        var checks = Checks().Select(c => c.CheckTypeId == CheckType.Stitching ? c with { ResultId = CheckResult.NotApplicable } : c).ToList();

        Assert.Throws<DomainException>(() => Record(Batch(), Input(checks: checks)));
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    public void Record_FailedItemWithoutDefect_Refused()
    {
        var error = Assert.Throws<DomainException>(() => Record(Batch(), Input(checks: Checks(CheckType.Stitching))));

        Assert.Contains("log at least one defect", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    public void Record_FailedItemWithDefectOfThatType_Accepted()
    {
        var inspection = Record(Batch(), Input(checks: Checks(CheckType.Stitching), defects: [Defect(DefectSeverity.Minor, 2, DefectType.Stitching)]));

        Assert.Single(inspection.Defects);
    }

    [Theory]
    [Trait("Spec", "AC-17")]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_DefectWithoutNote_Refused(string note)
    {
        Assert.Throws<DomainException>(() => Record(Batch(), Input(defects: [Defect(DefectSeverity.Minor, 1, note: note)])));
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    public void Record_DefectWithBadSeverityOrQuantity_Refused()
    {
        Assert.Throws<DomainException>(() => Record(Batch(), Input(defects: [Defect(9, 1)])));
        Assert.Throws<DomainException>(() => Record(Batch(), Input(defects: [Defect(DefectSeverity.Minor, 0)])));
        Assert.Throws<DomainException>(() => Record(Batch(), Input(defects: [Defect(DefectSeverity.Minor, 81)]))); // more than the 80 inspected
    }

    [Fact]
    [Trait("Spec", "AC-17")]
    public void Record_DefectOnASizeNotInTheBatch_Refused()
    {
        var defect = new DefectInput(DefectType.Stitching, DefectSeverity.Minor, 1, SizeId: 9, ColourId: 99, "torn seam");

        Assert.Throws<DomainException>(() => Record(Batch(), Input(defects: [defect])));
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public void Result_AtAcceptNo_Pass()
    {
        var inspection = Record(Batch(), Input(defects: [Defect(DefectSeverity.Minor, 5)]));

        Assert.Equal(InspectionResult.Pass, inspection.ResultId);
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public void Result_AtRejectNo_Fail()
    {
        var inspection = Record(Batch(), Input(defects: [Defect(DefectSeverity.Minor, 6)]));

        Assert.Equal(InspectionResult.Fail, inspection.ResultId);
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public void Result_AnyCritical_Fail()
    {
        var inspection = Record(Batch(), Input(defects: [Defect(DefectSeverity.Critical, 1)]));

        Assert.Equal(InspectionResult.Fail, inspection.ResultId);
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public void Record_SampleAboveLotOrBelowPlan_Refused_AndLargerSampleAllowed()
    {
        Assert.Throws<DomainException>(() => Record(Batch(), Input(sample: 1001)));
        Assert.Throws<DomainException>(() => Record(Batch(), Input(sample: 79)));
        Assert.Equal(120, Record(Batch(), Input(sample: 120)).SampleQty);
    }

    [Fact]
    [Trait("Spec", "AC-14")]
    public void Record_SmallLot_IsFullInspection()
    {
        var batch = Batch(line1: 30, line2: 15);

        var inspection = Record(batch, Input());

        Assert.Equal(InspectionMode.Full, inspection.ModeId);
        Assert.Equal(45, inspection.SampleQty);
    }

    [Fact]
    [Trait("Spec", "AC-30")]
    public void Record_NoActingPerson_Refused()
    {
        Assert.Throws<DomainException>(() => Record(Batch(), Input(person: " ")));
        Assert.Throws<DomainException>(() => Record(Batch(), Input(person: "A")));
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public void Record_FutureDate_Refused()
    {
        Assert.Throws<DomainException>(() => Record(Batch(), Input(date: Today.AddDays(1))));
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public void Record_SecondWholeBatchInspection_Refused()
    {
        var batch = Batch();
        Record(batch, Input());

        Assert.Throws<DomainException>(() => Record(batch, Input()));
    }

    [Fact]
    [Trait("Spec", "AC-16")]
    public void Record_UnknownSubBatch_NotFound()
    {
        var batch = Batch();

        Assert.Throws<KeyNotFoundException>(() => Record(batch, Input() with { SubBatchId = 55 }));
    }

    [Fact]
    [Trait("Spec", "AC-18")]
    public void Record_RaisesTheInspectionEvent_ForTheScorecard()
    {
        var batch = Batch();

        Record(batch, Input(defects: [Defect(DefectSeverity.Major, 2), Defect(DefectSeverity.Minor, 3)]));

        var raised = Assert.IsType<QcInspectionRecordedEvent>(Assert.Single(batch.DomainEvents));
        Assert.Equal((1000, 80, 5), (raised.LotQty, raised.SampleQty, raised.DefectiveQty));
        Assert.Equal((0, 2, 3), (raised.CriticalCount, raised.MajorCount, raised.MinorCount));
        Assert.Equal(("Aql", "Pass", (short)1, 0L), (raised.ModeCode, raised.ResultCode, raised.RoundNo, raised.SubBatchId));
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public void OpenFor_CountedLines_LotIsTheirSum_AndNothingCountedGivesNoBatch()
    {
        var batch = Batch(line1: 90, line2: 0);

        Assert.Equal(90, batch.LotQty);
        Assert.Single(batch.Lines);
        Assert.Equal(BatchStatus.Pending, batch.StatusId);
        Assert.Null(QcBatch.OpenFor(1, 7, "PO-2026-00007", [((short)1, (short)10, 0)]));
    }
}
