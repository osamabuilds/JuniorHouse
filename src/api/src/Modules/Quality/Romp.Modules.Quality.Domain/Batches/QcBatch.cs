using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;
using Romp.Modules.Quality.Domain.SafetyChecks;

namespace Romp.Modules.Quality.Domain.Batches;

/// <summary>Everything an inspection needs beyond the plan (spec AC-16, AC-17, AC-30).</summary>
public sealed record InspectionInput(
    long? SubBatchId,
    DateOnly InspectionDate,
    string ActingPerson,
    int? SampleQty,
    short InspectionLevelId,
    short AqlLevelId,
    IReadOnlyList<CheckInput> Checks,
    IReadOnlyList<DefectInput> Defects);

/// <summary>Maps to QC.QC_BTCH_MAIN: the lot to inspect, one per goods receipt (Q8). Holds its sub-batches and inspection rounds.</summary>
public sealed class QcBatch : AggregateRoot<long>, IAuditable
{
    private readonly List<QcBatchLine> _lines = [];
    private readonly List<QcSubBatch> _subBatches = [];
    private readonly List<Inspection> _inspections = [];

    private QcBatch()
    {
        PoNo = string.Empty;
    }

    public long GrnId { get; private set; }

    public long PoId { get; private set; }

    public string PoNo { get; private set; }

    public short StatusId { get; private set; }

    /// <summary>The lot size: the sum of the counted lines.</summary>
    public int LotQty { get; private set; }

    public IReadOnlyCollection<QcBatchLine> Lines => _lines.AsReadOnly();

    public IReadOnlyCollection<QcSubBatch> SubBatches => _subBatches.AsReadOnly();

    public IReadOnlyCollection<Inspection> Inspections => _inspections.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    /// <summary>
    /// Opens the batch for a recorded goods receipt (spec AC-12): status Pending, one line per counted size and colour.
    /// Lines with nothing counted are left out; returns null when nothing was counted at all (there is nothing to inspect).
    /// </summary>
    public static QcBatch? OpenFor(long grnId, long poId, string poNo, IEnumerable<(short SizeId, short ColourId, int Qty)> countedLines)
    {
        var lines = countedLines.Where(l => l.Qty > 0).ToList();
        if (lines.Count == 0)
        {
            return null;
        }

        var batch = new QcBatch { GrnId = grnId, PoId = poId, PoNo = poNo, StatusId = BatchStatus.Pending, LotQty = lines.Sum(l => l.Qty) };
        batch._lines.AddRange(lines.Select(l => new QcBatchLine(l.SizeId, l.ColourId, l.Qty)));
        return batch;
    }

    /// <summary>The number of units an inspection covers: the whole lot, or one sub-batch returned from rework.</summary>
    public int ScopeQty(long? subBatchId) =>
        subBatchId is null ? LotQty : FindSubBatch(subBatchId.Value).Lines.Sum(l => l.Qty);

    /// <summary>
    /// Records an inspection round (spec AC-13..AC-18, AC-30). The plan comes from <see cref="AqlCalculator"/>; the result is
    /// computed by <see cref="InspectionScorer"/> and stored. Raises <c>QcInspectionRecorded</c> (scorecard input).
    /// </summary>
    public Inspection RecordInspection(InspectionInput input, AqlPlan plan, int criticalTolerance, DateOnly today)
    {
        if (StatusId == BatchStatus.Closed)
        {
            throw new DomainException("This batch is closed; no more inspections can be recorded.");
        }

        var person = input.ActingPerson?.Trim() ?? string.Empty;
        if (person.Length is < 2 or > 100)
        {
            throw new DomainException("Enter the name of the person recording this (2 to 100 characters).");
        }

        if (input.InspectionDate > today)
        {
            throw new DomainException($"The inspection date cannot be after today ({today:yyyy-MM-dd}).");
        }

        short roundNo;
        if (input.SubBatchId is null)
        {
            if (_inspections.Any(i => i.SubBatchId is null))
            {
                throw new DomainException("This batch has already been inspected; a rework re-inspection is recorded on the returned sub-batch.");
            }

            roundNo = 1;
        }
        else
        {
            var sub = FindSubBatch(input.SubBatchId.Value);
            if (sub.StatusId != BatchStatus.Pending)
            {
                throw new DomainException("That sub-batch is not waiting for a re-inspection.");
            }

            roundNo = (short)(_inspections.Count(i => i.SubBatchId == sub.Id) + 1);
        }

        var lot = ScopeQty(input.SubBatchId);
        var sample = input.SampleQty ?? plan.SampleQty;
        if (sample < 1 || sample > lot)
        {
            throw new DomainException($"The inspected quantity must be between 1 and the lot size ({lot}).");
        }

        if (sample < plan.SampleQty)
        {
            throw new DomainException($"The inspected quantity ({sample}) is below the sample size the AQL plan requires ({plan.SampleQty}).");
        }

        ValidateChecklist(input.Checks);
        ValidateDefects(input.Defects, input.Checks, sample);

        var score = InspectionScorer.Score(input.Defects, sample, plan.AcceptNo, plan.RejectNo, criticalTolerance);
        var inspection = new Inspection(
            input.SubBatchId, roundNo, plan.ModeId, input.InspectionLevelId, input.AqlLevelId, lot, sample, plan.AcceptNo, plan.RejectNo,
            score.ResultId, input.InspectionDate, person, input.Checks, input.Defects);
        _inspections.Add(inspection);

        if (StatusId == BatchStatus.Pending)
        {
            StatusId = BatchStatus.InInspection;
        }

        if (input.SubBatchId is { } subId)
        {
            FindSubBatch(subId).MarkInInspection();
        }

        Raise(new QcInspectionRecordedEvent(
            Id, input.SubBatchId ?? 0, GrnId, PoId, PoNo, roundNo, InspectionMode.Code(plan.ModeId), lot, sample, score.DefectiveQty,
            score.CriticalCount, score.MajorCount, score.MinorCount, InspectionResult.Code(score.ResultId)));
        return inspection;
    }

    /// <summary>
    /// Records both child-safety checks of an inspection round (spec AC-30..AC-33). Both must be present, Not applicable needs a
    /// reason, a failed check may list the affected lines (none listed means the whole batch, Q10). Recording again before any
    /// decision replaces the earlier entry (a correction); once the covered quantity is decided the results are frozen.
    /// </summary>
    public Inspection RecordSafetyChecks(long inspectionId, IReadOnlyList<SafetyInput> inputs, string actingPerson)
    {
        var inspection = _inspections.FirstOrDefault(i => i.Id == inspectionId)
            ?? throw new KeyNotFoundException($"Inspection {inspectionId} was not found on QC batch {Id}.");

        var person = actingPerson?.Trim() ?? string.Empty;
        if (person.Length is < 2 or > 100)
        {
            throw new DomainException("Enter the name of the person recording this (2 to 100 characters).");
        }

        var decided = inspection.SubBatchId is { } subId
            ? FindSubBatch(subId).OutcomeId is not null
            : _subBatches.Any(s => s.OutcomeId is not null);
        if (decided)
        {
            throw new DomainException("A decision has already been recorded for this quantity, so its safety checks can no longer change.");
        }

        foreach (var type in SafetyType.All)
        {
            var matching = inputs.Count(i => i.SafetyTypeId == type);
            if (matching != 1)
            {
                throw new DomainException(matching == 0
                    ? $"Record a result for both child-safety checks; check {type} is missing."
                    : $"Safety check {type} appears more than once.");
            }
        }

        if (inputs.Any(i => !SafetyType.All.Contains(i.SafetyTypeId)))
        {
            throw new DomainException("The safety checks contain an unknown check.");
        }

        foreach (var input in inputs)
        {
            if (input.ResultId is not (CheckResult.Pass or CheckResult.Fail or CheckResult.NotApplicable))
            {
                throw new DomainException("Each safety check is Pass, Fail or Not applicable.");
            }

            if (input.ResultId == CheckResult.NotApplicable && string.IsNullOrWhiteSpace(input.Reason))
            {
                throw new DomainException("Say why a safety check is not applicable (for example: this garment has no small trims).");
            }

            if (input.Reason is { } reason && reason.Trim().Length > 500)
            {
                throw new DomainException("The reason can be at most 500 characters.");
            }

            var scope = inspection.SubBatchId is { } sub ? FindSubBatch(sub).Lines.Select(l => (l.SizeId, l.ColourId)) : _lines.Select(l => (l.SizeId, l.ColourId));
            var scopeSet = scope.ToHashSet();
            foreach (var line in input.AffectedLines)
            {
                if (!scopeSet.Contains((line.SizeId, line.ColourId)))
                {
                    throw new DomainException($"Size {line.SizeId} and colour {line.ColourId} is not part of the inspected quantity.");
                }
            }
        }

        foreach (var input in inputs)
        {
            inspection.SetSafetyCheck(input, person);
        }

        return inspection;
    }

    private static void ValidateChecklist(IReadOnlyList<CheckInput> checks)
    {
        foreach (var type in CheckType.All)
        {
            var matching = checks.Count(c => c.CheckTypeId == type);
            if (matching != 1)
            {
                throw new DomainException(matching == 0
                    ? $"Record a result for every checklist item; check {type} is missing."
                    : $"Check {type} appears more than once.");
            }
        }

        if (checks.Any(c => !CheckType.All.Contains(c.CheckTypeId)))
        {
            throw new DomainException("The checklist contains an unknown item.");
        }

        if (checks.Any(c => c.ResultId is not (CheckResult.Pass or CheckResult.Fail)))
        {
            throw new DomainException("Each checklist item is either Pass or Fail.");
        }
    }

    private void ValidateDefects(IReadOnlyList<DefectInput> defects, IReadOnlyList<CheckInput> checks, int sample)
    {
        foreach (var defect in defects)
        {
            if (defect.DefectTypeId is < DefectType.Stitching or > DefectType.Other)
            {
                throw new DomainException("A defect has an unknown type.");
            }

            if (defect.SeverityId is not (DefectSeverity.Critical or DefectSeverity.Major or DefectSeverity.Minor))
            {
                throw new DomainException("Every defect needs a severity: Critical, Major or Minor.");
            }

            if (defect.Qty < 1 || defect.Qty > sample)
            {
                throw new DomainException($"A defect's quantity must be between 1 and the inspected quantity ({sample}).");
            }

            if (string.IsNullOrWhiteSpace(defect.Note) || defect.Note.Trim().Length > 500)
            {
                throw new DomainException("Every defect needs a written note (up to 500 characters).");
            }

            if ((defect.SizeId is null) != (defect.ColourId is null))
            {
                throw new DomainException("Give both size and colour for a defect, or neither.");
            }

            if (defect.SizeId is { } size && !_lines.Any(l => l.SizeId == size && l.ColourId == defect.ColourId))
            {
                throw new DomainException($"Size {size} and colour {defect.ColourId} is not part of this batch.");
            }
        }

        foreach (var failed in checks.Where(c => c.ResultId == CheckResult.Fail))
        {
            if (!defects.Any(d => d.DefectTypeId == DefectType.ForCheck(failed.CheckTypeId)))
            {
                throw new DomainException($"Check {failed.CheckTypeId} failed, so log at least one defect of that type with a note.");
            }
        }
    }

    private QcSubBatch FindSubBatch(long id) =>
        _subBatches.FirstOrDefault(s => s.Id == id) ?? throw new KeyNotFoundException($"Sub-batch {id} was not found on this batch.");
}

/// <summary>Maps to QC.QC_BTCH_LINE: the counted quantity of one size and colour in the batch.</summary>
public sealed class QcBatchLine : Entity<long>, IAuditable
{
    private QcBatchLine()
    {
    }

    internal QcBatchLine(short sizeId, short colourId, int qty)
    {
        SizeId = sizeId;
        ColourId = colourId;
        Qty = qty;
    }

    public long BatchId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int Qty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// Maps to QC.QC_BTCH_SPLT: a decided part of a batch. A whole-batch decision is one sub-batch (number 1). The
/// outcome is empty until it is decided.
/// </summary>
public sealed class QcSubBatch : Entity<long>, IAuditable
{
    private readonly List<QcSubBatchLine> _lines = [];
    private readonly List<ReworkReturn> _returns = [];

    private QcSubBatch()
    {
    }

    public long BatchId { get; private set; }

    public short SplitNo { get; private set; }

    public short? OutcomeId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? ActingPerson { get; private set; }

    /// <summary>How many times this part has been sent back for rework (rounds are unlimited).</summary>
    public short ReworkRoundCount { get; private set; }

    public short StatusId { get; private set; }

    public IReadOnlyCollection<QcSubBatchLine> Lines => _lines.AsReadOnly();

    public IReadOnlyCollection<ReworkReturn> Returns => _returns.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    internal void MarkInInspection() => StatusId = BatchStatus.InInspection;
}

/// <summary>Maps to QC.QC_BTCH_SPLT_LINE. <see cref="ReturnedQty"/> is the running total returned from the vendor after rework.</summary>
public sealed class QcSubBatchLine : Entity<long>, IAuditable
{
    private QcSubBatchLine()
    {
    }

    public long SubBatchId { get; private set; }

    public short SizeId { get; private set; }

    public short ColourId { get; private set; }

    public int Qty { get; private set; }

    public int ReturnedQty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>Maps to QC.QC_RWRK_RTRN: one return of reworked goods from the vendor.</summary>
public sealed class ReworkReturn : Entity<long>, IAuditable
{
    private ReworkReturn()
    {
        ActingPerson = string.Empty;
    }

    public long SubBatchId { get; private set; }

    public DateOnly ReturnDate { get; private set; }

    public string ActingPerson { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
