using Romp.BuildingBlocks.Domain;
using Romp.Modules.Production.Contracts;
using Romp.Modules.Production.Domain.Deliveries;
using Romp.Modules.Production.Domain.Milestones;
using Romp.Modules.Production.Domain.Samples;

namespace Romp.Modules.Production.Domain.ProductionRuns;

/// <summary>
/// Maps to PROD.PROD_RUN_MAIN (spec D9: one production run per PO). Opened when the PO is
/// acknowledged; holds the milestone timeline, PP-sample rounds, expected-completion history and
/// delivery notes. It stores no ordered quantities or prices - those come from the PO's terms
/// (Vendor.Contracts) when needed. Every state change raises its event in the same call, so both
/// land in one transaction (ADR 0004).
/// </summary>
public sealed class ProductionRun : AggregateRoot<long>, IAuditable
{
    private readonly List<Milestone> _milestones = [];
    private readonly List<PpSampleRound> _ppRounds = [];
    private readonly List<ExpectedCompletionChange> _expectedHistory = [];
    private readonly List<DeliveryNote> _deliveryNotes = [];

    private ProductionRun()
    {
        PoNo = string.Empty;
    }

    public ProductionRun(long poId, string poNo, short? fabricResponsibilityId, DateOnly poSentDate, DateOnly? latestAcceptableDate)
    {
        PoId = poId;
        PoNo = poNo;
        FabricResponsibilityId = fabricResponsibilityId;
        PoSentDate = poSentDate;
        LatestAcceptableDate = latestAcceptableDate;
        StatusId = RunStatus.Open;
        Raise(new ProductionRunOpenedEvent(0, poId, poNo));
    }

    public long PoId { get; private set; }

    public string PoNo { get; private set; }

    public short? FabricResponsibilityId { get; private set; }

    /// <summary>D11: no milestone or delivery can be dated before the PO was sent.</summary>
    public DateOnly PoSentDate { get; private set; }

    /// <summary>A copy of the PO's In-force latest acceptable date, refreshed by revision events (plan: read-model choice so the list flags "at risk" in one query).</summary>
    public DateOnly? LatestAcceptableDate { get; private set; }

    public short StatusId { get; private set; }

    public DateOnly? ExpectedCompletionDate { get; private set; }

    /// <summary>The claimed date of the (single) BulkCuttingStarted milestone.</summary>
    public DateOnly? CuttingStartedDate { get; private set; }

    /// <summary>The earliest recorded FabricDispatchedByRomp date (D14).</summary>
    public DateOnly? FabricDispatchDate { get; private set; }

    /// <summary>AC-36 (D14): cutting was recorded before Romp's fabric was dispatched. Loud, never blocking.</summary>
    public bool IsCuttingBeforeFabricDispatch { get; private set; }

    /// <summary>AC-27 (D17): a construction change came into force after PP approval; a new PP round is strongly recommended.</summary>
    public bool IsResampleRecommended { get; private set; }

    /// <summary>AC-38 (D17): cutting was recorded while a re-sample was still recommended.</summary>
    public bool IsCuttingWithoutResample { get; private set; }

    /// <summary>AC-13/AC-27: an amendment came into force after approval, so a new round is allowed again.</summary>
    public bool IsApprovalSuperseded { get; private set; }

    public IReadOnlyCollection<Milestone> Milestones => _milestones.AsReadOnly();

    public IReadOnlyCollection<PpSampleRound> PpRounds => _ppRounds.AsReadOnly();

    public IReadOnlyCollection<ExpectedCompletionChange> ExpectedHistory => _expectedHistory.AsReadOnly();

    public IReadOnlyCollection<DeliveryNote> DeliveryNotes => _deliveryNotes.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public bool IsOpen => StatusId == RunStatus.Open;

    public bool HasApprovedPpSample => _ppRounds.Any(r => r.DecisionId == PpDecision.Approved);

    public bool IsCuttingStarted => CuttingStartedDate is not null;

    /// <summary>AC-16: expected completion later than the latest acceptable date minus the buffer.</summary>
    public bool IsAtRisk(int bufferDays) =>
        ExpectedCompletionDate is { } expected && LatestAcceptableDate is { } latest && expected > latest.AddDays(-bufferDays);

    // ---------------------------------------------------------------- milestones

    /// <summary>
    /// AC-4..AC-9, AC-17, AC-30, AC-35, AC-36, AC-38. The three PP types are recorded by the sample-round
    /// methods instead. <paramref name="today"/> is the Pakistan-time date.
    /// </summary>
    public Milestone RecordMilestone(
        short typeId,
        DateOnly claimedDate,
        string reportedBy,
        string actingPerson,
        string? note,
        DateOnly today,
        int recordedLateDays,
        IReadOnlyCollection<(short SizeId, short ColourId, int Qty)>? finishedLines = null)
    {
        EnsureOpen("record milestones");
        RequirePerson(actingPerson);

        if (!MilestoneType.IsGeneric(typeId))
        {
            throw new DomainException("Sample approvals and rejections are recorded on the sample round, not as a plain milestone.");
        }

        if (typeId == MilestoneType.FabricBooked && FabricResponsibilityId != FabricResponsibility.VendorSupplied)
        {
            throw new DomainException($"Fabric booked applies only when the vendor supplies the fabric (PO {PoNo} does not).");
        }

        if (typeId == MilestoneType.FabricDispatchedByRomp && FabricResponsibilityId != FabricResponsibility.RompSupplied)
        {
            throw new DomainException($"Fabric dispatch applies only when Romp supplies the fabric (PO {PoNo} does not).");
        }

        EnsureDateInRange(claimedDate, today);

        if (typeId == MilestoneType.BulkCuttingStarted)
        {
            return RecordBulkCutting(claimedDate, reportedBy, actingPerson, note, today, recordedLateDays);
        }

        var milestone = AddMilestone(typeId, claimedDate, reportedBy, actingPerson, note, today, recordedLateDays, ppRoundId: null);

        if (typeId == MilestoneType.FabricDispatchedByRomp && (FabricDispatchDate is null || claimedDate < FabricDispatchDate))
        {
            FabricDispatchDate = claimedDate;
        }

        if (typeId == MilestoneType.FinishedReadyToShip && finishedLines is { Count: > 0 })
        {
            if (finishedLines.Any(l => l.Qty <= 0))
            {
                throw new DomainException("Finished quantities must be positive whole numbers.");
            }

            milestone.AddFinishedLines(finishedLines);
        }

        return milestone;
    }

    private Milestone RecordBulkCutting(DateOnly claimedDate, string reportedBy, string actingPerson, string? note, DateOnly today, int recordedLateDays)
    {
        if (!HasApprovedPpSample)
        {
            throw new DomainException(
                $"Bulk cutting cannot be recorded for PO {PoNo} until a pre-production sample has been approved. Submit a sample and approve it first.");
        }

        if (CuttingStartedDate is not null)
        {
            throw new DomainException($"Bulk cutting has already been recorded for PO {PoNo}.");
        }

        var milestone = AddMilestone(MilestoneType.BulkCuttingStarted, claimedDate, reportedBy, actingPerson, note, today, recordedLateDays, ppRoundId: null);
        CuttingStartedDate = claimedDate;

        var flags = new List<string>();

        // AC-36 / D14: Romp-supplied fabric that had not been dispatched by the cutting date.
        if (FabricResponsibilityId == FabricResponsibility.RompSupplied
            && !_milestones.Any(m => m.TypeId == MilestoneType.FabricDispatchedByRomp && m.ClaimedDate <= claimedDate))
        {
            IsCuttingBeforeFabricDispatch = true;
            flags.Add("Cutting started before Romp's fabric was dispatched.");
        }

        // AC-38 / D17: a construction change came into force after approval and no new sample followed.
        if (IsResampleRecommended)
        {
            IsCuttingWithoutResample = true;
            flags.Add("Cutting started without re-sampling after a construction change.");
        }

        if (flags.Count > 0)
        {
            milestone.SetFlagReason(string.Join(" ", flags));
        }

        Raise(new ProductionStartedEvent(Id, PoId, PoNo, claimedDate));
        return milestone;
    }

    // ---------------------------------------------------------------- PP sample

    /// <summary>AC-11 / AC-13: a new round. One pending at a time; after an approval only when an amendment superseded it.</summary>
    public PpSampleRound SubmitPpSample(DateOnly submittedDate, string reportedBy, string actingPerson, string? note, DateOnly today, int recordedLateDays)
    {
        EnsureOpen("submit a pre-production sample");
        actingPerson = RequirePerson(actingPerson);
        EnsureDateInRange(submittedDate, today);

        if (_ppRounds.Any(r => r.IsPending))
        {
            throw new DomainException("A sample is already waiting for a decision. Approve or reject it before submitting another.");
        }

        if (HasApprovedPpSample && !IsApprovalSuperseded)
        {
            throw new DomainException(
                "A pre-production sample was already approved. A new round is only allowed after an amendment to the PO.");
        }

        var round = new PpSampleRound(Id, (_ppRounds.Count == 0 ? 0 : _ppRounds.Max(r => r.RoundNo)) + 1, submittedDate, actingPerson, note);
        _ppRounds.Add(round);
        IsResampleRecommended = false;
        IsApprovalSuperseded = false;

        AddMilestone(MilestoneType.PpSampleSubmitted, submittedDate, reportedBy, actingPerson, note, today, recordedLateDays, ppRoundId: null);
        return round;
    }

    /// <summary>AC-10 / AC-11: approve or reject the pending round. Rejection needs a reason.</summary>
    public PpSampleRound DecidePpSample(int roundNo, bool approve, string actingPerson, string? reason, DateOnly today, DateTimeOffset now)
    {
        EnsureOpen("decide a pre-production sample");
        actingPerson = RequirePerson(actingPerson);

        var round = _ppRounds.FirstOrDefault(r => r.RoundNo == roundNo)
            ?? throw new KeyNotFoundException($"Sample round {roundNo} was not found on the production run for PO {PoNo}.");

        if (!round.IsPending)
        {
            throw new DomainException($"Sample round {roundNo} has already been decided.");
        }

        if (!approve && string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A reason is required when a sample is rejected.");
        }

        round.Decide(approve ? PpDecision.Approved : PpDecision.Rejected, actingPerson, reason?.Trim(), now);
        AddMilestone(
            approve ? MilestoneType.PpSampleApproved : MilestoneType.PpSampleRejected,
            today, reportedBy: "Romp", actingPerson, reason, today, recordedLateDays: int.MaxValue, ppRoundId: round.Id == 0 ? null : round.Id);
        return round;
    }

    // ---------------------------------------------------------------- expected completion

    /// <summary>AC-15 / AC-16: set or move the expected completion date; the history is kept.</summary>
    public ExpectedCompletionChange SetExpectedCompletion(DateOnly newDate, string? reason, string actingPerson)
    {
        EnsureOpen("set the expected completion date");
        actingPerson = RequirePerson(actingPerson);

        if (!IsCuttingStarted)
        {
            throw new DomainException("The expected completion date can be set once bulk cutting has started.");
        }

        var change = new ExpectedCompletionChange(Id, ExpectedCompletionDate, newDate, reason?.Trim(), actingPerson);
        _expectedHistory.Add(change);
        ExpectedCompletionDate = newDate;
        return change;
    }

    // ---------------------------------------------------------------- deliveries

    /// <summary>
    /// AC-18..AC-24, AC-31, AC-37. <paramref name="terms"/> must be the terms in force on
    /// <paramref name="dispatchDate"/> (D12). Over-delivery is recorded and flagged, never refused (D4).
    /// </summary>
    public DeliveryNote RecordDeliveryNote(
        string vendorDnNo,
        DateOnly dispatchDate,
        bool isFinalShipment,
        IReadOnlyList<NoteLineInput> lines,
        DeliveryTerms terms,
        string actingPerson,
        DateOnly today)
    {
        EnsureOpen("record a delivery note");
        actingPerson = RequirePerson(actingPerson);

        var dnNo = (vendorDnNo ?? string.Empty).Trim();
        if (dnNo.Length == 0)
        {
            throw new DomainException("The vendor's delivery note number is required.");
        }

        if (!IsCuttingStarted)
        {
            throw new DomainException($"PO {PoNo} is not in production yet - record bulk cutting before a delivery note.");
        }

        EnsureDateInRange(dispatchDate, today);

        if (_deliveryNotes.Any(n => string.Equals(n.VendorDnNo, dnNo, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Delivery note {dnNo} has already been recorded for PO {PoNo}.");
        }

        if (lines.Count == 0 || lines.Any(l => l.Qty <= 0))
        {
            throw new DomainException("Every delivery line needs a positive whole quantity.");
        }

        if (lines.GroupBy(l => (l.SizeId, l.ColourId)).Any(g => g.Count() > 1))
        {
            throw new DomainException("Each size and colour can appear only once on a delivery note.");
        }

        if (lines.Any(l => terms.Lines.All(o => o.SizeId != l.SizeId || o.ColourId != l.ColourId)))
        {
            throw new DomainException($"A delivery line uses a size or colour that is not on PO {PoNo}.");
        }

        var shippedBefore = ShippedToDate();
        var judgement = DeliveryJudge.Judge(terms, shippedBefore, lines, dispatchDate, isFinalShipment);

        var rompFabricDelay = FabricResponsibilityId == FabricResponsibility.RompSupplied
            && (FabricDispatchDate is null || CuttingStartedDate < FabricDispatchDate);

        var note = new DeliveryNote(
            Id, dnNo, dispatchDate, isFinalShipment, terms.RevisionNumber, judgement,
            FabricResponsibilityId == FabricResponsibility.RompSupplied ? FabricDispatchDate : null, rompFabricDelay, actingPerson);
        _deliveryNotes.Add(note);

        Raise(new DeliveryNoteRecordedEvent(
            Id, PoId, PoNo, dnNo, dispatchDate, isFinalShipment,
            judgement.PoIsDelivered ? "Delivered" : "PartiallyDelivered",
            DeliveryTiming.Code(judgement.TimingId), judgement.DaysLate,
            DeliveryQuantityStatus.Code(judgement.QuantityStatusId), judgement.OverQty, judgement.ShortQty,
            terms.RevisionNumber, rompFabricDelay,
            judgement.Lines.Select(l => new DeliveredLine(l.SizeId, l.ColourId, l.Qty)).ToList()));

        return note;
    }

    /// <summary>Requires <see cref="DeliveryNotes"/> and their lines to be loaded.</summary>
    public Dictionary<(short SizeId, short ColourId), int> ShippedToDate() =>
        _deliveryNotes.SelectMany(n => n.Lines)
            .GroupBy(l => (l.SizeId, l.ColourId))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Qty));

    // ---------------------------------------------------------------- reactions to PO events

    /// <summary>AC-16 / AC-27: a revision came into force. Refreshes the date copy; after an approval it also supersedes it, and a construction change recommends re-sampling.</summary>
    public void ApplyRevisionInForce(DateOnly? latestAcceptableDate, bool isConstructionChange)
    {
        LatestAcceptableDate = latestAcceptableDate;

        if (!HasApprovedPpSample)
        {
            return;
        }

        IsApprovalSuperseded = true;
        if (isConstructionChange && !_ppRounds.Any(r => r.IsPending))
        {
            IsResampleRecommended = true;
        }
    }

    /// <summary>AC-29: idempotent. A cancelled run accepts no more milestones or notes.</summary>
    public bool MarkCancelled()
    {
        if (StatusId != RunStatus.Open)
        {
            return false;
        }

        StatusId = RunStatus.Cancelled;
        Raise(new ProductionRunCancelledEvent(Id, PoId, PoNo));
        return true;
    }

    /// <summary>D2: the PO was closed manually; the run closes with it. Idempotent.</summary>
    public bool MarkClosed()
    {
        if (StatusId != RunStatus.Open)
        {
            return false;
        }

        StatusId = RunStatus.Closed;
        Raise(new ProductionRunClosedEvent(Id, PoId, PoNo));
        return true;
    }

    // ---------------------------------------------------------------- helpers

    private Milestone AddMilestone(
        short typeId, DateOnly claimedDate, string reportedBy, string actingPerson, string? note, DateOnly today, int recordedLateDays, long? ppRoundId)
    {
        var recordedLate = recordedLateDays != int.MaxValue && today.DayNumber - claimedDate.DayNumber > recordedLateDays;
        var milestone = new Milestone(Id, typeId, claimedDate, reportedBy.Trim(), actingPerson.Trim(), note?.Trim(), recordedLate, ppRoundId);
        _milestones.Add(milestone);
        Raise(new ProductionMilestoneReachedEvent(Id, PoId, PoNo, MilestoneType.Code(typeId), claimedDate));
        return milestone;
    }

    /// <summary>D11 / AC-6: between the PO's Send date and today, inclusive.</summary>
    private void EnsureDateInRange(DateOnly date, DateOnly today)
    {
        if (date < PoSentDate || date > today)
        {
            throw new DomainException($"The date must be between {PoSentDate:yyyy-MM-dd} (when PO {PoNo} was sent) and today ({today:yyyy-MM-dd}).");
        }
    }

    private void EnsureOpen(string action)
    {
        if (StatusId != RunStatus.Open)
        {
            throw new DomainException($"The production run for PO {PoNo} is {RunStatus.Code(StatusId).ToLowerInvariant()} and cannot {action}.");
        }
    }

    private static string RequirePerson(string actingPerson)
    {
        var name = actingPerson?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 100)
        {
            throw new DomainException("Enter the name of the person recording this (2 to 100 characters).");
        }

        return name;
    }
}

/// <summary>Maps to PROD.PROD_EXPC_HIST: append-only history of the expected completion date (AC-15).</summary>
public sealed class ExpectedCompletionChange : Entity<long>, IAuditable
{
    private ExpectedCompletionChange()
    {
        ActingPerson = string.Empty;
    }

    internal ExpectedCompletionChange(long runId, DateOnly? oldDate, DateOnly newDate, string? reason, string actingPerson)
    {
        RunId = runId;
        OldDate = oldDate;
        NewDate = newDate;
        Reason = reason;
        ActingPerson = actingPerson;
    }

    public long RunId { get; private set; }

    public DateOnly? OldDate { get; private set; }

    public DateOnly NewDate { get; private set; }

    public string? Reason { get; private set; }

    public string ActingPerson { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
