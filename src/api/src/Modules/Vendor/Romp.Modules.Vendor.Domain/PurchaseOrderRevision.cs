using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain;

/// <summary>
/// Maps to VNDR.PO_REV (ADR 0007). An immutable snapshot of every amendable term plus computed
/// impact figures, taken at creation - only its <see cref="StatusId"/> changes afterward, via
/// <see cref="PoRevisionStatusHistoryEntry"/> (AC-22: no public method here ever mutates a snapshot
/// or impact-figure property after construction - <see cref="MarkSuperseded"/> is the only allowed
/// post-creation change, and it touches only <see cref="StatusId"/> and the status history).
/// Created only via <see cref="PurchaseOrder.CreateRevision"/>, which owns the revision-number
/// allocation and impact-figure computation (SCRUM-93 task 20).
/// </summary>
public sealed class PurchaseOrderRevision : Entity<long>, IAuditable
{
    private readonly List<PoRevisionStatusHistoryEntry> _statusHistory = [];
    private readonly List<PoRevisionLine> _lines = [];

    private PurchaseOrderRevision()
    {
        ImpactNote = string.Empty;
    }

    internal PurchaseOrderRevision(
        long poId,
        short revisionNumber,
        short initiatorId,
        short statusId,
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
        decimal poValueBefore,
        decimal poValueAfter,
        decimal poValueDiff,
        decimal advanceAmountBefore,
        decimal advanceAmountAfter,
        int expectedDateShiftDays,
        int? latestAcceptableDateShiftDays,
        int quantityDiff,
        bool isBeyondLatestAcceptableDate,
        IEnumerable<(short SizeId, short ColourId, int Qty)> lines)
    {
        PoId = poId;
        RevisionNumber = revisionNumber;
        InitiatorId = initiatorId;
        StatusId = statusId;
        ReasonId = reasonId;
        ImpactNote = impactNote;
        VendorMessage = vendorMessage;
        UnitCost = unitCost;
        ExpectedDeliveryDate = expectedDeliveryDate;
        LatestAcceptableDate = latestAcceptableDate;
        OverTolerancePercent = overTolerancePercent;
        UnderTolerancePercent = underTolerancePercent;
        PaymentTermId = paymentTermId;
        AdvancePercent = advancePercent;
        FabricResponsibilityId = fabricResponsibilityId;
        PoValueBefore = poValueBefore;
        PoValueAfter = poValueAfter;
        PoValueDiff = poValueDiff;
        AdvanceAmountBefore = advanceAmountBefore;
        AdvanceAmountAfter = advanceAmountAfter;
        ExpectedDateShiftDays = expectedDateShiftDays;
        LatestAcceptableDateShiftDays = latestAcceptableDateShiftDays;
        QuantityDiff = quantityDiff;
        IsBeyondLatestAcceptableDate = isBeyondLatestAcceptableDate;

        _lines.AddRange(lines.Select(line => new PoRevisionLine(Id, line.SizeId, line.ColourId, line.Qty)));
        _statusHistory.Add(new PoRevisionStatusHistoryEntry(Id, fromStatusId: null, statusId, note: null));
    }

    /// <summary>AC-9: a later revision going immediately In-force supersedes this one in the same call.</summary>
    internal void MarkSuperseded()
    {
        _statusHistory.Add(new PoRevisionStatusHistoryEntry(Id, StatusId, RevisionStatus.Superseded, note: null));
        StatusId = RevisionStatus.Superseded;
    }

    public long PoId { get; private set; }

    public short RevisionNumber { get; private set; }

    public short InitiatorId { get; private set; }

    public short StatusId { get; private set; }

    public short ReasonId { get; private set; }

    public string ImpactNote { get; private set; }

    public string? VendorMessage { get; private set; }

    // Terms snapshot (mirrors PO_MAIN's own commercial-terms columns at the moment this revision was created).
    public decimal UnitCost { get; private set; }

    public DateOnly ExpectedDeliveryDate { get; private set; }

    public DateOnly? LatestAcceptableDate { get; private set; }

    public decimal? OverTolerancePercent { get; private set; }

    public decimal? UnderTolerancePercent { get; private set; }

    public short PaymentTermId { get; private set; }

    public decimal AdvancePercent { get; private set; }

    public short? FabricResponsibilityId { get; private set; }

    // Computed impact figures (computed once at creation, never recalculated - plan.md's Domain model section).
    public decimal PoValueBefore { get; private set; }

    public decimal PoValueAfter { get; private set; }

    public decimal PoValueDiff { get; private set; }

    public decimal AdvanceAmountBefore { get; private set; }

    public decimal AdvanceAmountAfter { get; private set; }

    public int ExpectedDateShiftDays { get; private set; }

    public int? LatestAcceptableDateShiftDays { get; private set; }

    public int QuantityDiff { get; private set; }

    public bool IsBeyondLatestAcceptableDate { get; private set; }

    public IReadOnlyCollection<PoRevisionStatusHistoryEntry> StatusHistory => _statusHistory.AsReadOnly();

    public IReadOnlyCollection<PoRevisionLine> Lines => _lines.AsReadOnly();

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
