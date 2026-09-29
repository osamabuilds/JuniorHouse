using Romp.BuildingBlocks.Storage;

namespace Romp.Modules.Vendor.Application.Files;

/// <summary>SCRUM-93 task 34/35. The shared storage root (see <see cref="FileStorageOptions"/>) plus the PO upload limits (AC-45), overridable via <c>Vndr:PoFileStorage:*</c> config.</summary>
public sealed class PoFileStorageOptions : FileStorageOptions
{
    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;

    public int MaxFilesPerPo { get; init; } = 25;
}
