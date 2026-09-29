using Romp.Modules.Vendor.Domain.Vendors;

namespace Romp.Modules.Vendor.Application.Revisions;

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
    IReadOnlyCollection<PoRevisionLineDto> Lines,
    IReadOnlyCollection<PoRevisionCommunicationDto>? Communications = null);

/// <summary>SCRUM-93 task 47 (AC-31, AC-34): how and by whom the vendor's response or request behind a revision was captured. TypeId is PO_VNDR_COMM_TYP_LKP (Confirmed/Countered/Declined/AmendmentRequest/Decision).</summary>
/// <summary>An evidence file behind a communication (internal - staff only).</summary>
public sealed record PoEvidenceDto(long FileId, string FileName);

public sealed record PoRevisionCommunicationDto(
    short TypeId, short ChannelId, string ResponderName, DateTimeOffset ResponseDte, IReadOnlyCollection<PoEvidenceDto>? Evidence = null);
