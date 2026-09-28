namespace Romp.Modules.Vendor.Application;

public sealed record PoRevisionLineDto(short SizeId, short ColourId, int Qty);

/// <summary>Full detail of one revision (AC-18, AC-19, AC-22, AC-23) - an immutable snapshot plus computed impact figures.</summary>
public sealed record PoRevisionDto(
    long Id,
    long PoId,
    short RevisionNumber,
    short InitiatorId,
    short StatusId,
    short ReasonId,
    string ImpactNote,
    string? VendorMessage,
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    DateOnly? LatestAcceptableDate,
    decimal? OverTolerancePercent,
    decimal? UnderTolerancePercent,
    short PaymentTermId,
    decimal AdvancePercent,
    short? FabricResponsibilityId,
    decimal PoValueBefore,
    decimal PoValueAfter,
    decimal PoValueDiff,
    decimal AdvanceAmountBefore,
    decimal AdvanceAmountAfter,
    int ExpectedDateShiftDays,
    int? LatestAcceptableDateShiftDays,
    int QuantityDiff,
    bool IsBeyondLatestAcceptableDate,
    IReadOnlyCollection<PoRevisionLineDto> Lines);
