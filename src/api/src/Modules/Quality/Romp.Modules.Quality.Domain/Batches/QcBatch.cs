using Romp.BuildingBlocks.Domain;
using Romp.Modules.Quality.Contracts;
using Romp.Modules.Quality.Domain.Inspections;
using Romp.Modules.Quality.Domain.Inspections.Aql;
using Romp.Modules.Quality.Domain.Outcomes;
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
            if (sub.StatusId != BatchStatus.Pending || _inspections.Any(i => i.SubBatchId == sub.Id))
            {
                throw new DomainException("That sub-batch is not waiting for a re-inspection.");
            }

            // The returned quantity's first inspection is the next round after the rework it came back from.
            roundNo = (short)(sub.ReworkRoundCount + 1);
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

    /// <summary>
    /// Records the first decision on the batch, whole or split (spec AC-19..AC-25, AC-31, AC-32). The batch must have been
    /// inspected; the parts must add up exactly to the counted quantity (<see cref="BatchSplitPlanner"/>); every outcome other
    /// than Accepted needs a reason; and Accepted / Concession pass the child-safety gate (<see cref="SafetyGate"/>). A part
    /// that is accepted is released at once, whatever happens to the other parts (AC-23). Returns the new sub-batches; the
    /// caller saves, then calls <see cref="RaiseOutcomeEvent"/> for each so the events carry the sub-batch ids.
    /// </summary>
    public IReadOnlyList<QcSubBatch> Decide(IReadOnlyList<DecisionPart> parts, string actingPerson, DateTimeOffset now)
    {
        if (StatusId == BatchStatus.Closed)
        {
            throw new DomainException("This batch is closed.");
        }

        if (_subBatches.Count > 0)
        {
            throw new DomainException("This batch has already been decided; a decided part cannot be decided again.");
        }

        var inspection = _inspections.FirstOrDefault(i => i.SubBatchId is null)
            ?? throw new DomainException("Record an inspection before recording an outcome for this batch.");
        var person = ValidatePerson(actingPerson);

        BatchSplitPlanner.Validate(_lines.Select(l => (l.SizeId, l.ColourId, l.Qty)).ToList(), parts);

        foreach (var part in parts)
        {
            ValidateOutcome(part.OutcomeId, part.Reason);
            SafetyGate.EnsureCanRelease(part.OutcomeId, inspection, part.Lines.Select(l => new LineRef(l.SizeId, l.ColourId)).ToList());
        }

        var created = new List<QcSubBatch>();
        for (var i = 0; i < parts.Count; i++)
        {
            var sub = QcSubBatch.Decided((short)(i + 1), parts[i], person, now, parentId: null, reworkRounds: 0);
            _subBatches.Add(sub);
            created.Add(sub);
        }

        RefreshStatus();
        return created;
    }

    /// <summary>
    /// Decides a returned rework quantity after its re-inspection (spec AC-26, AC-34): one outcome for the whole returned
    /// part, through the same reason and safety rules. Rework again sends it back for another round; rounds are unlimited.
    /// </summary>
    public QcSubBatch DecideSubBatch(long subBatchId, short outcomeId, string? reason, string actingPerson, DateTimeOffset now)
    {
        var sub = FindSubBatch(subBatchId);
        var person = ValidatePerson(actingPerson);

        switch (sub.StatusId)
        {
            case BatchStatus.Pending:
                throw new DomainException("Record the re-inspection of this returned part before recording an outcome.");
            case BatchStatus.AwaitingRework:
                throw new DomainException("This part is still with the vendor for rework; record its return first.");
            case BatchStatus.Decided:
                throw new DomainException("This part has already been decided; a decided part cannot be decided again.");
        }

        ValidateOutcome(outcomeId, reason);
        var inspection = _inspections.Where(i => i.SubBatchId == sub.Id).OrderByDescending(i => i.RoundNo).FirstOrDefault();
        SafetyGate.EnsureCanRelease(outcomeId, inspection, sub.Lines.Select(l => new LineRef(l.SizeId, l.ColourId)).ToList());

        sub.Decide(outcomeId, reason, person, now);
        RefreshStatus();
        return sub;
    }

    /// <summary>
    /// Records the vendor returning reworked goods (spec AC-26, AC-27): per size and colour, never more than was sent. The
    /// returned units become a new part awaiting re-inspection; whatever has not come back stays visible as outstanding on the
    /// rework part, neither rejected nor accepted. Returns the new part.
    /// </summary>
    public QcSubBatch RecordReworkReturn(long subBatchId, DateOnly returnDate, string actingPerson, string? note, IReadOnlyList<PartLine> lines, DateOnly today)
    {
        var sub = FindSubBatch(subBatchId);
        var person = ValidatePerson(actingPerson);

        if (sub.OutcomeId != QcOutcome.Rework || sub.StatusId != BatchStatus.AwaitingRework)
        {
            throw new DomainException("Only a part that was sent back for rework and has not fully returned can receive a return.");
        }

        if (returnDate > today)
        {
            throw new DomainException($"The return date cannot be after today ({today:yyyy-MM-dd}).");
        }

        if (lines.Count == 0 || lines.Any(l => l.Qty < 1))
        {
            throw new DomainException("Enter the returned quantity for at least one size and colour (whole numbers above 0).");
        }

        if (lines.GroupBy(l => (l.SizeId, l.ColourId)).Any(g => g.Count() > 1))
        {
            throw new DomainException("Each size and colour can appear only once in a return.");
        }

        foreach (var line in lines)
        {
            var sent = sub.Lines.FirstOrDefault(l => l.SizeId == line.SizeId && l.ColourId == line.ColourId)
                ?? throw new DomainException($"Size {line.SizeId} and colour {line.ColourId} was not sent for rework.");
            if (line.Qty > sent.OutstandingQty)
            {
                throw new DomainException(
                    $"Size {line.SizeId} and colour {line.ColourId}: {line.Qty} returned but only {sent.OutstandingQty} of the {sent.Qty} sent is still outstanding.");
            }
        }

        var returned = QcSubBatch.Returned((short)(_subBatches.Max(s => s.SplitNo) + 1), sub, lines);
        sub.AddReturn(returnDate, person, note, lines);
        _subBatches.Add(returned);
        RefreshStatus();
        return returned;
    }

    /// <summary>Raises the event for a decided part (spec AC-29): accepted/concession, rejected, or rework requested. Call after the part has been saved, so it carries its id.</summary>
    public void RaiseOutcomeEvent(QcSubBatch sub)
    {
        var lines = sub.Lines.Select(l => new QcLine(l.SizeId, l.ColourId, l.Qty)).ToList();
        switch (sub.OutcomeId)
        {
            case QcOutcome.Accepted or QcOutcome.Concession:
                Raise(new QcBatchAcceptedEvent(Id, sub.Id, GrnId, PoId, PoNo, QcOutcome.Code(sub.OutcomeId.Value), lines));
                break;
            case QcOutcome.Rejected:
                Raise(new QcBatchRejectedEvent(Id, sub.Id, GrnId, PoId, PoNo, lines));
                break;
            case QcOutcome.Rework:
                Raise(new QcBatchReworkRequestedEvent(Id, sub.Id, GrnId, PoId, PoNo, lines));
                break;
        }
    }

    private void RefreshStatus()
    {
        if (_subBatches.Count == 0)
        {
            return;
        }

        StatusId = _subBatches.Any(s => s.StatusId is BatchStatus.Pending or BatchStatus.InInspection or BatchStatus.AwaitingRework)
            ? BatchStatus.AwaitingRework
            : BatchStatus.Decided;
    }

    private static string ValidatePerson(string actingPerson)
    {
        var person = actingPerson?.Trim() ?? string.Empty;
        return person.Length is < 2 or > 100
            ? throw new DomainException("Enter the name of the person recording this (2 to 100 characters).")
            : person;
    }

    private static void ValidateOutcome(short outcomeId, string? reason)
    {
        if (outcomeId is not (QcOutcome.Accepted or QcOutcome.Concession or QcOutcome.Rejected or QcOutcome.Rework))
        {
            throw new DomainException("The outcome must be Accept, Accept with concession, Reject or Rework.");
        }

        if (outcomeId != QcOutcome.Accepted && string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Give a reason for a concession, a rejection or a rework.");
        }

        if (reason is { } text && text.Trim().Length > 500)
        {
            throw new DomainException("The reason can be at most 500 characters.");
        }
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

