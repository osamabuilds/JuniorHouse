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
    public void Send()
    {
        RequireStatus(PoStatus.Draft, "send");
        StatusId = PoStatus.SentToVendor;
        _statusHistory.Add(new PoStatusHistoryEntry(Id, PoStatus.SentToVendor, cancelReasonId: null));
        Raise(new PoSentToVendorEvent(Id, PoNo));
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
    public void Cancel(short cancelReasonId)
    {
        if (StatusId == PoStatus.Cancelled)
        {
            throw new DomainException($"PO {PoNo} is already cancelled.");
        }

        StatusId = PoStatus.Cancelled;
        _statusHistory.Add(new PoStatusHistoryEntry(Id, PoStatus.Cancelled, cancelReasonId));
        Raise(new PoCancelledEvent(Id, PoNo, cancelReasonId));
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
