
namespace Romp.Modules.Vendor.Application.Files;

/// <summary>SCRUM-93 task 38 (AC-44). Revision numbers, not ids, are what staff see; null = added at Draft stage / never retired.</summary>
public sealed record PoFileDto(
    long Id,
    string FileName,
    short CategoryId,
    bool IsVendorVisible,
    long FileSizeBytes,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    short? AddedInRevisionNumber,
    short? RetiredInRevisionNumber);
