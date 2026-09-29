namespace Romp.Modules.Production.Application.ProductionRuns;

/// <summary>One row of the production list (spec AC-32): projected in SQL, no children loaded.</summary>
public sealed record RunSummaryDto(
    long Id,
    long PoId,
    string PoNo,
    short StatusId,
    short? FabricResponsibilityId,
    DateOnly? ExpectedCompletionDate,
    DateOnly? LatestAcceptableDate,
    bool IsAtRisk,
    bool IsCuttingStarted,
    bool HasApprovedSample,
    bool IsCuttingBeforeFabricDispatch,
    bool IsResampleRecommended,
    bool IsCuttingWithoutResample,
    int ShippedQty,
    bool HasOverDelivery,
    bool HasShortDelivery,
    bool HasLateDelivery);

public sealed record MilestoneDto(
    long Id,
    short TypeId,
    string TypeCode,
    DateOnly ClaimedDate,
    string ReportedBy,
    string ActingPerson,
    string? Note,
    bool IsRecordedLate,
    string? FlagReason,
    long? PpRoundId,
    DateTimeOffset RecordedAt,
    string RecordedBy,
    IReadOnlyList<QuantityLineDto> FinishedLines);

public sealed record QuantityLineDto(short SizeId, short ColourId, int Qty);

public sealed record PpSampleFileDto(long Id, string FileName, string ContentType, long FileSizeBytes, DateTimeOffset UploadedAt);

public sealed record PpSampleRoundDto(
    long Id,
    int RoundNo,
    DateOnly SubmittedDate,
    string SubmittedBy,
    string? Note,
    short DecisionId,
    string DecisionCode,
    DateTimeOffset? DecidedAt,
    string? DecidedBy,
    string? DecisionReason,
    IReadOnlyList<PpSampleFileDto> Files);

public sealed record ExpectedCompletionChangeDto(DateOnly? OldDate, DateOnly NewDate, string? Reason, string ActingPerson, DateTimeOffset ChangedAt);

public sealed record DeliveryNoteLineDto(short SizeId, short ColourId, int Qty, int OverQty, int ShortQty);

public sealed record DeliveryNoteDto(
    long Id,
    string VendorDnNo,
    DateOnly DispatchDate,
    bool IsFinalShipment,
    short RevisionNumber,
    short TimingId,
    string TimingCode,
    int DaysLate,
    short QuantityStatusId,
    string QuantityStatusCode,
    int OverQty,
    int ShortQty,
    DateOnly? FabricDispatchDate,
    bool IsRompFabricDelay,
    string ActingPerson,
    DateTimeOffset RecordedAt,
    IReadOnlyList<DeliveryNoteLineDto> Lines);

/// <summary>Ordered vs shipped per size x colour (AC-19), with the tolerance limits the judgement uses.</summary>
public sealed record OrderedVsShippedDto(short SizeId, short ColourId, int Ordered, int Shipped, int MinRequired, int MaxAllowed);

/// <summary>
/// The PO facts a run's screen needs - deliberately not the PO's cost, payment terms, internal
/// notes or files (spec AC-34).
/// </summary>
public sealed record RunPoTermsDto(
    short RevisionNumber,
    DateOnly ExpectedDeliveryDate,
    DateOnly? LatestAcceptableDate,
    decimal? OverTolerancePercent,
    decimal? UnderTolerancePercent);

public sealed record RunDetailDto(
    long Id,
    long PoId,
    string PoNo,
    short StatusId,
    short? FabricResponsibilityId,
    DateOnly PoSentDate,
    DateOnly? ExpectedCompletionDate,
    DateOnly? LatestAcceptableDate,
    DateOnly? CuttingStartedDate,
    DateOnly? FabricDispatchDate,
    bool IsAtRisk,
    bool HasApprovedSample,
    bool IsCuttingBeforeFabricDispatch,
    bool IsResampleRecommended,
    bool IsCuttingWithoutResample,
    bool CanSubmitSample,
    RunPoTermsDto? PoTerms,
    IReadOnlyList<MilestoneDto> Milestones,
    IReadOnlyList<PpSampleRoundDto> PpRounds,
    IReadOnlyList<ExpectedCompletionChangeDto> ExpectedHistory,
    IReadOnlyList<DeliveryNoteDto> DeliveryNotes,
    IReadOnlyList<OrderedVsShippedDto> Quantities);
