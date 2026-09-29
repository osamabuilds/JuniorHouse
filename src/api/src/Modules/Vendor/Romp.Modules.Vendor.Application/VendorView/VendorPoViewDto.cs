using Romp.Modules.Vendor.Application.PurchaseOrders;

namespace Romp.Modules.Vendor.Application.VendorView;

/// <summary>A vendor-visible file in effect at the revision being shown.</summary>
public sealed record VendorPoFileDto(long Id, string FileName, short CategoryId);

/// <summary>What a vendor is shown about an open, not-yet-decided proposal - never its internal impact note or computed impact figures.</summary>
public sealed record VendorPendingRevisionDto(
    short RevisionNumber,
    short InitiatorId,
    string? VendorMessage,
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    DateOnly? LatestAcceptableDate);

/// <summary>
/// SCRUM-93 task 42 (AC-35, AC-36). The read-only view a vendor sees of a PO. Deliberately
/// hand-authored rather than derived from <see cref="PoDto"/>/<see cref="PoRevisionDto"/>: it has
/// no field for the internal impact note, impact figures, internal files, vendor evidence or any
/// target/retail price, so none of them can reach the vendor by a mapping mistake. A reflection
/// test (<c>VendorPoViewDtoTests</c>) keeps it that way.
/// </summary>
public sealed record VendorPoViewDto(
    string PoNo,
    string VendorName,
    long StyleId,
    short StatusId,
    short RevisionNumber,
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    DateOnly? LatestAcceptableDate,
    decimal? OverTolerancePercent,
    decimal? UnderTolerancePercent,
    short PaymentTermId,
    decimal AdvancePercent,
    short? FabricResponsibilityId,
    IReadOnlyCollection<PoLineDto> Lines,
    IReadOnlyCollection<VendorPoFileDto> Files,
    VendorPendingRevisionDto? PendingRevision);
