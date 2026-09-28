using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain;

/// <summary>
/// Maps to VNDR.PO_MAIN (FR-SC-02, §5.9). This sprint's slice of the state machine:
/// Draft -&gt; SentToVendor -&gt; Acknowledged, or Cancelled from any of those three (AC-7, AC-10,
/// AC-11, AC-12). Every transition appends one PoStatusHistoryEntry and raises one domain event
/// in the same call, so both land in the same SaveChanges transaction (ADR 0004).
/// </summary>
public sealed class PurchaseOrder : AggregateRoot<long>, IAuditable
{
    private readonly List<PoLine> _lines = [];
    private readonly List<PoStatusHistoryEntry> _statusHistory = [];
    private readonly List<PurchaseOrderRevision> _revisions = [];
    private readonly List<PoVendorCommunication> _vendorCommunications = [];

    private PurchaseOrder()
    {
        PoNo = string.Empty;
    }

    public PurchaseOrder(
        string poNo,
        long vendorId,
        long styleId,
        decimal unitCost,
        DateOnly expectedDeliveryDate,
        short paymentTermId,
        decimal advancePercent,
        IEnumerable<(short SizeId, short ColourId, int Qty)> lines)
    {
        PoNo = poNo;
        VendorId = vendorId;
        StyleId = styleId;
        UnitCost = unitCost;
        ExpectedDeliveryDate = expectedDeliveryDate;
        PaymentTermId = paymentTermId;
        AdvancePercent = advancePercent;
        StatusId = PoStatus.Draft;

        _lines.AddRange(lines.Select(line => new PoLine(Id, line.SizeId, line.ColourId, line.Qty)));
        _statusHistory.Add(new PoStatusHistoryEntry(Id, PoStatus.Draft, cancelReasonId: null));
        Raise(new PoCreatedEvent(Id, poNo, vendorId, styleId));
    }

    public string PoNo { get; private set; }

    public long VendorId { get; private set; }

    public long StyleId { get; private set; }

    public decimal UnitCost { get; private set; }

    public DateOnly ExpectedDeliveryDate { get; private set; }

    public short PaymentTermId { get; private set; }

    public decimal AdvancePercent { get; private set; }

    /// <summary>SCRUM-93 task 16 (R10, no FR id - logged as BRD errata). Null on every PO until set by <see cref="UpdateDraftDetails"/> - Sprint 1 POs never invent a value (AC-7).</summary>
    public DateOnly? LatestAcceptableDate { get; private set; }

    public decimal? OverTolerancePercent { get; private set; }

    public decimal? UnderTolerancePercent { get; private set; }

    public short? FabricResponsibilityId { get; private set; }

    public short StatusId { get; private set; }

    public IReadOnlyCollection<PoLine> Lines => _lines.AsReadOnly();

    /// <summary>Oldest first (AC-14).</summary>
    public IReadOnlyCollection<PoStatusHistoryEntry> StatusHistory => _statusHistory.AsReadOnly();

    /// <summary>ADR 0007. Requires the caller to have loaded this navigation (e.g. `.Include(p =&gt; p.Revisions)`) before calling <see cref="CreateRevision"/> - the MAX+1 allocation below reads this in-memory collection, not a fresh query.</summary>
    public IReadOnlyCollection<PurchaseOrderRevision> Revisions => _revisions.AsReadOnly();

    /// <summary>SCRUM-93 task 30 (AC-31): every vendor response/counter/decision, with channel/responder/time captured.</summary>
    public IReadOnlyCollection<PoVendorCommunication> VendorCommunications => _vendorCommunications.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }

    public bool IsDraft => StatusId == PoStatus.Draft;

    /// <summary>
    /// AC-9: Draft only. AC-13: anything past Draft rejects with a domain error. AC-8 (task 16-18):
    /// the 4 new commercial-terms fields save in place here too, the same as the Sprint 1 fields -
    /// they consume no revision (revisions arrive with the ADR 0007 work later in this sprint).
    /// </summary>
    public void UpdateDraftDetails(
        decimal unitCost,
        DateOnly expectedDeliveryDate,
        short paymentTermId,
        decimal advancePercent,
        DateOnly? latestAcceptableDate,
        decimal? overTolerancePercent,
        decimal? underTolerancePercent,
        short? fabricResponsibilityId,
        IEnumerable<(short SizeId, short ColourId, int Qty)> lines)
    {
        RequireStatus(PoStatus.Draft, "edit");

        UnitCost = unitCost;
        ExpectedDeliveryDate = expectedDeliveryDate;
        PaymentTermId = paymentTermId;
        AdvancePercent = advancePercent;
        LatestAcceptableDate = latestAcceptableDate;
        OverTolerancePercent = overTolerancePercent;
        UnderTolerancePercent = underTolerancePercent;
        FabricResponsibilityId = fabricResponsibilityId;

        _lines.Clear();
        _lines.AddRange(lines.Select(line => new PoLine(Id, line.SizeId, line.ColourId, line.Qty)));
    }

    /// <summary>AC-10: Draft -&gt; SentToVendor.</summary>
    /// <remarks>SCRUM-93 task 18/task 63 (AC-63): also creates a Rev 0 baseline snapshot in this same call, so every Sent-and-later PO (new or backfilled by task 21's migration) has a real In-force revision to compare/acknowledge against.</remarks>
    public void Send()
    {
        RequireStatus(PoStatus.Draft, "send");
        StatusId = PoStatus.SentToVendor;
        _statusHistory.Add(new PoStatusHistoryEntry(Id, PoStatus.SentToVendor, cancelReasonId: null));
        CreateBaselineRevision();
        Raise(new PoSentToVendorEvent(Id, PoNo));
    }

    /// <summary>AC-63: Rev 0, captured as-is (no diff from itself) - not raised as its own outbox event; plan.md's events table only ties PoRevisionPutInForceEvent to CreateAmendmentCommand/DecideRevisionCommand, not Send.</summary>
    private void CreateBaselineRevision()
    {
        var poValue = _lines.Sum(l => l.Qty) * UnitCost;
        var advanceAmount = poValue * AdvancePercent / 100m;
        var isBeyondLatestAcceptableDate = LatestAcceptableDate.HasValue && ExpectedDeliveryDate > LatestAcceptableDate.Value;

        var revision = new PurchaseOrderRevision(
            Id, revisionNumber: 0, initiatorId: 1 /* AmendmentInitiator.Buyer */, statusId: RevisionStatus.InForce, reasonId: 11 /* AmendmentReason.Other */,
            impactNote: "Baseline revision captured at Send.", vendorMessage: null,
            UnitCost, ExpectedDeliveryDate, LatestAcceptableDate, OverTolerancePercent, UnderTolerancePercent,
            PaymentTermId, AdvancePercent, FabricResponsibilityId,
            poValueBefore: poValue, poValueAfter: poValue, poValueDiff: 0m,
            advanceAmountBefore: advanceAmount, advanceAmountAfter: advanceAmount,
            expectedDateShiftDays: 0, latestAcceptableDateShiftDays: null, quantityDiff: 0, isBeyondLatestAcceptableDate,
            _lines.Select(l => (l.SizeId, l.ColourId, l.Qty)));

        _revisions.Add(revision);
    }

    /// <summary>AC-11: SentToVendor -&gt; Acknowledged.</summary>
    public void Acknowledge()
    {
        RequireStatus(PoStatus.SentToVendor, "acknowledge");
        StatusId = PoStatus.Acknowledged;
        _statusHistory.Add(new PoStatusHistoryEntry(Id, PoStatus.Acknowledged, cancelReasonId: null));
        Raise(new PoAcknowledgedEvent(Id, PoNo));
    }

    /// <summary>AC-12: Draft/SentToVendor/Acknowledged -&gt; Cancelled, with a mandatory reason (AC-12a is enforced by the command validator, not here).</summary>
    /// <remarks>SCRUM-93 task 26 (AC-21): the caller must have loaded <see cref="Revisions"/> - an open Pending revision is auto-withdrawn in this same call.</remarks>
    public void Cancel(short cancelReasonId)
    {
        if (StatusId == PoStatus.Cancelled)
        {
            throw new DomainException($"PO {PoNo} is already cancelled.");
        }

        var openPending = _revisions.FirstOrDefault(r => r.StatusId == RevisionStatus.Pending);
        if (openPending is not null)
        {
            openPending.MarkWithdrawn("Auto-withdrawn: PO cancelled.");
            Raise(new PoRevisionWithdrawnEvent(Id, PoNo, openPending.RevisionNumber));
        }

        StatusId = PoStatus.Cancelled;
        _statusHistory.Add(new PoStatusHistoryEntry(Id, PoStatus.Cancelled, cancelReasonId));
        Raise(new PoCancelledEvent(Id, PoNo, cancelReasonId));
    }

    /// <summary>
    /// SCRUM-93 task 20 (ADR 0007). Allocates the next revision number as MAX(existing)+1 from this
    /// aggregate's own already-loaded <see cref="Revisions"/> - the caller (task 22's handler) is
    /// responsible for deciding <paramref name="goesImmediatelyInForce"/> from the PO's own status
    /// (AC-9: SentToVendor -&gt; true; AC-10: Acknowledged -&gt; false) and for the no-op/reason/note
    /// checks (AC-16, AC-17) before calling this. Also computes this revision's immutable impact
    /// figures (AC-18, AC-19) from this aggregate's own current (before) state.
    /// </summary>
    /// <remarks>
    /// The caller must have loaded both <see cref="Revisions"/> (for the MAX+1 allocation) and
    /// <see cref="Lines"/> (same requirement as <see cref="UpdateDraftDetails"/>) before calling this
    /// - when <paramref name="goesImmediatelyInForce"/> is true, this mirrors the new lines onto
    /// <see cref="Lines"/> by clearing and re-adding, same as <see cref="UpdateDraftDetails"/>; if
    /// the old lines were never loaded, EF never sees them as removed and the new lines' insert can
    /// collide with the untouched old rows on (PoId, SizeId, ColourId).
    /// </remarks>
    public PurchaseOrderRevision CreateRevision(
        short initiatorId,
        short reasonId,
        string impactNote,
        string? vendorMessage,
        decimal unitCost,
        DateOnly expectedDeliveryDate,
        DateOnly? latestAcceptableDate,
        decimal? overTolerancePercent,
        decimal? underTolerancePercent,
        short paymentTermId,
        decimal advancePercent,
        short? fabricResponsibilityId,
        IReadOnlyCollection<(short SizeId, short ColourId, int Qty)> lines,
        bool goesImmediatelyInForce)
    {
        var revisionNumber = (short)((_revisions.Count == 0 ? 0 : _revisions.Max(r => r.RevisionNumber)) + 1);
        var initialStatusId = goesImmediatelyInForce ? RevisionStatus.InForce : RevisionStatus.Pending;

        var poValueBefore = _lines.Sum(l => l.Qty) * UnitCost;
        var poValueAfter = lines.Sum(l => l.Qty) * unitCost;
        var advanceAmountBefore = poValueBefore * AdvancePercent / 100m;
        var advanceAmountAfter = poValueAfter * advancePercent / 100m;
        var expectedDateShiftDays = expectedDeliveryDate.DayNumber - ExpectedDeliveryDate.DayNumber;
        int? latestAcceptableDateShiftDays = LatestAcceptableDate.HasValue && latestAcceptableDate.HasValue
            ? latestAcceptableDate.Value.DayNumber - LatestAcceptableDate.Value.DayNumber
            : null;
        var quantityDiff = lines.Sum(l => l.Qty) - _lines.Sum(l => l.Qty);
        var isBeyondLatestAcceptableDate = latestAcceptableDate.HasValue && expectedDeliveryDate > latestAcceptableDate.Value;

        var revision = new PurchaseOrderRevision(
            Id, revisionNumber, initiatorId, initialStatusId, reasonId, impactNote, vendorMessage,
            unitCost, expectedDeliveryDate, latestAcceptableDate, overTolerancePercent, underTolerancePercent,
            paymentTermId, advancePercent, fabricResponsibilityId,
            poValueBefore, poValueAfter, poValueAfter - poValueBefore,
            advanceAmountBefore, advanceAmountAfter,
            expectedDateShiftDays, latestAcceptableDateShiftDays, quantityDiff, isBeyondLatestAcceptableDate,
            lines);

        // AC-9: the previous In-force revision becomes Superseded in the same call that creates the
        // new one - only relevant when the new revision itself goes immediately In-force.
        if (goesImmediatelyInForce)
        {
            PutRevisionInForce(revision, lines);
        }
        else
        {
            Raise(new PoRevisionProposedEvent(Id, PoNo, revisionNumber));
        }

        _revisions.Add(revision);
        return revision;
    }

    /// <summary>
    /// SCRUM-93 task 24 (AC-11). Accepts an existing Pending revision - same effect as a new revision
    /// going immediately In-force (AC-9), reused here since both mean "this revision's terms become
    /// the PO's current position in this same transaction".
    /// </summary>
    /// <remarks>The caller must have loaded both <see cref="Revisions"/> and <see cref="Lines"/> - see <see cref="CreateRevision"/>'s own remarks.</remarks>
    public void AcceptPendingRevision(short revisionNumber)
    {
        var revision = FindRevisionOrThrow(revisionNumber);
        RequireRevisionStatus(revision, RevisionStatus.Pending, "accepted");

        revision.MarkInForce();
        PutRevisionInForce(revision, revision.Lines.Select(l => (l.SizeId, l.ColourId, l.Qty)));
    }

    /// <summary>SCRUM-93 task 24 (AC-12). Rejects an existing Pending revision - the PO's position is unchanged, and it stays visible in history.</summary>
    public void RejectPendingRevision(short revisionNumber, string? note)
    {
        var revision = FindRevisionOrThrow(revisionNumber);
        RequireRevisionStatus(revision, RevisionStatus.Pending, "rejected");

        revision.MarkRejected(note);
        Raise(new PoRevisionRejectedEvent(Id, PoNo, revisionNumber));
    }

    /// <summary>SCRUM-93 task 25 (AC-13). Withdrawn by its own proposer - never takes effect. Also used by <see cref="Cancel"/>'s auto-withdraw (task 26, AC-21).</summary>
    public void WithdrawPendingRevision(short revisionNumber, string? note)
    {
        var revision = FindRevisionOrThrow(revisionNumber);
        RequireRevisionStatus(revision, RevisionStatus.Pending, "withdrawn");

        revision.MarkWithdrawn(note);
        Raise(new PoRevisionWithdrawnEvent(Id, PoNo, revisionNumber));
    }

    /// <summary>Mirrors <paramref name="revision"/>'s terms/lines onto this aggregate's own row and supersedes whichever other revision was previously In-force - shared by both a new revision created immediately In-force (AC-9) and an existing Pending revision being accepted (AC-11).</summary>
    private void PutRevisionInForce(PurchaseOrderRevision revision, IEnumerable<(short SizeId, short ColourId, int Qty)> lines)
    {
        var priorInForce = _revisions.FirstOrDefault(r => r.StatusId == RevisionStatus.InForce && r.RevisionNumber != revision.RevisionNumber);
        if (priorInForce is not null)
        {
            priorInForce.MarkSuperseded();
            Raise(new PoRevisionSupersededEvent(Id, PoNo, priorInForce.RevisionNumber));
        }

        // ADR 0007: the parent's mirror is updated only by the same transaction that moves a
        // revision to InForce - this real property change on the aggregate's own row is also what
        // protects a concurrent amendment's MAX+1 allocation (AC-24): a race is caught by the xmin
        // check this update triggers, not a separate mechanism.
        UnitCost = revision.UnitCost;
        ExpectedDeliveryDate = revision.ExpectedDeliveryDate;
        LatestAcceptableDate = revision.LatestAcceptableDate;
        OverTolerancePercent = revision.OverTolerancePercent;
        UnderTolerancePercent = revision.UnderTolerancePercent;
        PaymentTermId = revision.PaymentTermId;
        AdvancePercent = revision.AdvancePercent;
        FabricResponsibilityId = revision.FabricResponsibilityId;

        _lines.Clear();
        _lines.AddRange(lines.Select(line => new PoLine(Id, line.SizeId, line.ColourId, line.Qty)));

        Raise(new PoRevisionPutInForceEvent(Id, PoNo, revision.RevisionNumber));
    }

    /// <summary>
    /// SCRUM-93 task 30 (AC-31). <paramref name="revisionId"/> must already be a real (saved) id -
    /// pass an existing revision's id directly, or, for a revision created in the same handler call
    /// (task 30's Countered path via <see cref="CreateRevision"/>), save once first so its id is
    /// realized before calling this (this entity is added directly to this aggregate's own
    /// collection, not fixed up through a navigation to the revision).
    /// </summary>
    public PoVendorCommunication RecordVendorCommunication(
        short communicationTypeId, short channelId, string responderName, DateTimeOffset responseDte, long? revisionId)
    {
        var communication = new PoVendorCommunication(Id, revisionId, communicationTypeId, channelId, responderName, responseDte);
        _vendorCommunications.Add(communication);
        return communication;
    }

    private PurchaseOrderRevision FindRevisionOrThrow(short revisionNumber) =>
        _revisions.FirstOrDefault(r => r.RevisionNumber == revisionNumber)
            ?? throw new DomainException($"PO {PoNo} has no revision {revisionNumber}.");

    private void RequireRevisionStatus(PurchaseOrderRevision revision, short requiredStatusId, string action)
    {
        if (revision.StatusId != requiredStatusId)
        {
            throw new DomainException($"Revision {revision.RevisionNumber} of PO {PoNo} cannot be {action} while it isn't Pending.");
        }
    }

    private void RequireStatus(short requiredStatusId, string action)
    {
        if (StatusId != requiredStatusId)
        {
            throw new DomainException(
                $"PO {PoNo} cannot be {action}ed while in its current status (only Draft -> Sent -> Acknowledged is a legal edit/send/acknowledge path; use amendment or cancellation instead).");
        }
    }
}
