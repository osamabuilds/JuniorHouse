namespace Romp.Modules.Vendor.Application;

/// <summary>Full vendor detail (FR-SC-01, AC-5).</summary>
public sealed record VendorDto(
    long Id,
    string Name,
    string ContactName,
    string ContactPhone,
    string? ContactEmail,
    short CityId,
    short PaymentTermId,
    decimal? OnTimePercent,
    decimal? OnQuantityPercent,
    decimal? DefectRatePercent,
    bool IsActive,
    IReadOnlyCollection<short> SpecialisationIds);

/// <summary>Row shape for the vendor search/list screen.</summary>
public sealed record VendorSummaryDto(long Id, string Name, short CityId, bool IsActive);
