namespace Romp.Modules.Vendor.Contracts;

/// <summary>
/// SCRUM-93 task 46 (AC-51). The read-only questions another module (Sprint 3's production
/// tracking first) may ask about a purchase order, per ADR 0002 - it depends on this Contracts
/// project only, never on Vendor's Domain/Application/Infrastructure. None of the shapes here
/// carries an internal impact note, vendor evidence or an internal file.
/// </summary>
public interface IPurchaseOrderQueries
{
    /// <summary>The terms currently in force (and what they were agreed as). Null for an unknown or never-sent PO.</summary>
    Task<PoInForceTerms?> GetInForceTermsAsync(long poId, CancellationToken cancellationToken);

    /// <summary>Every revision, oldest first, so a consumer can follow how the terms moved.</summary>
    Task<IReadOnlyList<PoRevisionSummary>> ListRevisionsAsync(long poId, CancellationToken cancellationToken);

    /// <summary>The vendor-visible files in effect at <paramref name="revisionNumber"/> (the in-force revision when null).</summary>
    Task<IReadOnlyList<PoEffectiveFile>> GetEffectiveFilesAsync(long poId, short? revisionNumber, CancellationToken cancellationToken);
}

public sealed record PoTermsLineContract(short SizeId, short ColourId, int Qty);

public sealed record PoInForceTerms(
    long PoId,
    string PoNo,
    long VendorId,
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
    IReadOnlyList<PoTermsLineContract> Lines);

public sealed record PoRevisionSummary(
    short RevisionNumber,
    short StatusId,
    short InitiatorId,
    short ReasonId,
    decimal UnitCost,
    DateOnly ExpectedDeliveryDate,
    DateOnly? LatestAcceptableDate,
    DateTimeOffset CreatedAt);

public sealed record PoEffectiveFile(long FileId, string FileName, short CategoryId);
