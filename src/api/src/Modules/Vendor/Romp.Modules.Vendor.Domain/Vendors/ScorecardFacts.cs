using Romp.BuildingBlocks.Domain;

namespace Romp.Modules.Vendor.Domain.Vendors;

/// <summary>
/// Maps to VNDR.SCRD_DLVR: one row per vendor delivery note, the facts the on-time and on-quantity measures come from
/// (FR-SC-01, spec AC-35). Built from PROD's DeliveryNoteRecorded event; unique by (PO, DN number) so a redelivered event
/// is counted once (AC-38).
/// </summary>
public sealed class ScorecardDelivery : Entity<long>, IAuditable
{
    private ScorecardDelivery()
    {
        VendorDnNo = string.Empty;
    }

    public ScorecardDelivery(long vendorId, long poId, string vendorDnNo, DateOnly dispatchDate, bool onTime, bool onQuantity, bool rompFabricDelay)
    {
        VendorId = vendorId;
        PoId = poId;
        VendorDnNo = vendorDnNo;
        DispatchDate = dispatchDate;
        IsOnTime = onTime;
        IsOnQuantity = onQuantity;
        IsRompFabricDelay = rompFabricDelay;
    }

    public long VendorId { get; private set; }

    public long PoId { get; private set; }

    public string VendorDnNo { get; private set; }

    public DateOnly DispatchDate { get; private set; }

    public bool IsOnTime { get; private set; }

    public bool IsOnQuantity { get; private set; }

    /// <summary>A delay Romp caused (fabric dispatched after cutting): the delivery is left out of the on-time measure (AC-36).</summary>
    public bool IsRompFabricDelay { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}

/// <summary>
/// Maps to VNDR.SCRD_INSP: one row per QC batch, the first-pass inspection the defect rate comes from (Q2 default:
/// re-inspections after rework are not counted). Built from the QC QcInspectionRecorded event; unique by batch (AC-38).
/// </summary>
public sealed class ScorecardInspection : Entity<long>, IAuditable
{
    private ScorecardInspection()
    {
    }

    public ScorecardInspection(long vendorId, long poId, long batchId, DateOnly inspectionDate, int inspectedQty, int defectiveQty)
    {
        VendorId = vendorId;
        PoId = poId;
        BatchId = batchId;
        InspectionDate = inspectionDate;
        InspectedQty = inspectedQty;
        DefectiveQty = defectiveQty;
    }

    public long VendorId { get; private set; }

    public long PoId { get; private set; }

    /// <summary>QC batch id: a plain reference, never a foreign key across schemas.</summary>
    public long BatchId { get; private set; }

    public DateOnly InspectionDate { get; private set; }

    public int InspectedQty { get; private set; }

    public int DefectiveQty { get; private set; }

    public DateTimeOffset InsrDte { get; set; }

    public string InsrBy { get; set; } = string.Empty;

    public DateTimeOffset? UpdtDte { get; set; }

    public string? UpdtBy { get; set; }
}
