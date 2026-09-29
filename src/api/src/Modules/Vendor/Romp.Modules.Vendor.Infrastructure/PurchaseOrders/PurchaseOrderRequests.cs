using Romp.Modules.Vendor.Application.PurchaseOrders;

namespace Romp.Modules.Vendor.Infrastructure.PurchaseOrders;

/// <summary>PUT request body for editing a Draft PO - the id comes from the route.</summary>
public sealed record UpdatePoRequest(
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    short PaymentTermId,
    decimal AdvancePercent,
    IReadOnlyCollection<PoLineDto> Lines,
    DateOnly? LatestAcceptableDate = null,
    decimal? OverTolerancePercent = null,
    decimal? UnderTolerancePercent = null,
    short? FabricResponsibilityId = null)
{
    public UpdatePurchaseOrderCommand ToCommand(long id) => new(
        id, UnitCost, ExpectedDeliveryDate, PaymentTermId, AdvancePercent, Lines,
        LatestAcceptableDate, OverTolerancePercent, UnderTolerancePercent, FabricResponsibilityId);
}

public sealed record CancelPoRequest(short CancelReasonId);
