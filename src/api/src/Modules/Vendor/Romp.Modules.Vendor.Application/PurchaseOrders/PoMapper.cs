using Romp.Modules.Vendor.Domain.PurchaseOrders;

namespace Romp.Modules.Vendor.Application.PurchaseOrders;

internal static class PoMapper
{
    public static PoDto ToDto(this PurchaseOrder po) => new(
        po.Id,
        po.PoNo,
        po.VendorId,
        po.StyleId,
        po.UnitCost,
        po.ExpectedDeliveryDate,
        po.PaymentTermId,
        po.AdvancePercent,
        po.LatestAcceptableDate,
        po.OverTolerancePercent,
        po.UnderTolerancePercent,
        po.FabricResponsibilityId,
        po.StatusId,
        po.Lines.Select(l => new PoLineDto(l.SizeId, l.ColourId, l.Qty)).ToList(),
        po.StatusHistory
            .OrderBy(h => h.InsrDte)
            .Select(h => new PoStatusHistoryDto(h.PoStatusId, h.CancelReasonId, h.InsrDte, h.InsrBy, h.Note))
            .ToList());

    public static PoSummaryDto ToSummaryDto(this PurchaseOrder po) => new(po.Id, po.PoNo, po.VendorId, po.StatusId, po.ExpectedDeliveryDate);
}
