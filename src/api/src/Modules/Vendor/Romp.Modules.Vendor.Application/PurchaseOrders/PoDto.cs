
namespace Romp.Modules.Vendor.Application.PurchaseOrders;

public sealed record PoLineDto(short SizeId, short ColourId, int Qty);

public sealed record PoStatusHistoryDto(short PoStatusId, short? CancelReasonId, DateTimeOffset InsrDte, string InsrBy, string? Note = null);

/// <summary>Full PO detail, including the status timeline oldest-first (AC-14).</summary>
public sealed record PoDto(
    long Id,
    string PoNo,
    long VendorId,
    long StyleId,
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    short PaymentTermId,
    decimal AdvancePercent,
    DateOnly? LatestAcceptableDate,
    decimal? OverTolerancePercent,
    decimal? UnderTolerancePercent,
    short? FabricResponsibilityId,
    short StatusId,
    IReadOnlyCollection<PoLineDto> Lines,
    IReadOnlyCollection<PoStatusHistoryDto> StatusHistory);

/// <summary>Row shape for the purchase-order search/list screen.</summary>
public sealed record PoSummaryDto(long Id, string PoNo, long VendorId, short StatusId, DateOnly ExpectedDeliveryDate);
