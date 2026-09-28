using Romp.Modules.Vendor.Domain;

namespace Romp.Modules.Vendor.Application;

internal static class PoRevisionMapper
{
    public static PoRevisionDto ToDto(this PurchaseOrderRevision revision) => new(
        revision.Id,
        revision.PoId,
        revision.RevisionNumber,
        revision.InitiatorId,
        revision.StatusId,
        revision.ReasonId,
        revision.ImpactNote,
        revision.VendorMessage,
        revision.UnitCost,
        revision.ExpectedDeliveryDate,
        revision.LatestAcceptableDate,
        revision.OverTolerancePercent,
        revision.UnderTolerancePercent,
        revision.PaymentTermId,
        revision.AdvancePercent,
        revision.FabricResponsibilityId,
        revision.PoValueBefore,
        revision.PoValueAfter,
        revision.PoValueDiff,
        revision.AdvanceAmountBefore,
        revision.AdvanceAmountAfter,
        revision.ExpectedDateShiftDays,
        revision.LatestAcceptableDateShiftDays,
        revision.QuantityDiff,
        revision.IsBeyondLatestAcceptableDate,
        revision.Lines.Select(l => new PoRevisionLineDto(l.SizeId, l.ColourId, l.Qty)).ToList());
}
