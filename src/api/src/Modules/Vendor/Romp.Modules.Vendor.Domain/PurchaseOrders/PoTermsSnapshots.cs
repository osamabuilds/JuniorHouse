using Romp.Modules.Vendor.Contracts;
using Romp.Modules.Vendor.Domain.Revisions;

namespace Romp.Modules.Vendor.Domain.PurchaseOrders;

/// <summary>Builds the event terms snapshot (a Contracts type) from a domain revision.</summary>
internal static class PoTermsSnapshots
{
    public static PoTermsSnapshot From(PurchaseOrderRevision revision) => new(
        revision.UnitCost, revision.ExpectedDeliveryDate, revision.LatestAcceptableDate,
        revision.OverTolerancePercent, revision.UnderTolerancePercent, revision.PaymentTermId,
        revision.AdvancePercent, revision.FabricResponsibilityId,
        revision.Lines.Select(l => new PoTermsLine(l.SizeId, l.ColourId, l.Qty)).ToList());
}
