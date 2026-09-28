using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain;

/// <summary>
/// Maps to VNDR.PO_REV (ADR 0007). An immutable snapshot of every amendable term plus computed
/// impact figures, taken at creation - only its <see cref="StatusId"/> changes afterward, via
/// <see cref="PoRevisionStatusHistoryEntry"/> (task 20 adds the creation/transition behaviour and
/// the revision-number allocation rule; this shape is task 19's schema-only skeleton).
/// </summary>
public sealed class PurchaseOrderRevision : Entity<long>, IAuditable
{
    private readonly List<PoRevisionStatusHistoryEntry> _statusHistory = [];
    private readonly List<PoRevisionLine> _lines = [];

    private PurchaseOrderRevision()
    {
        ImpactNote = string.Empty;
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
