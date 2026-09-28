namespace Romp.Modules.Vendor.Application;

/// <summary>SCRUM-93 task 34/35. Where the local-disk <see cref="IFileStorage"/> implementation (Infrastructure) writes PO file bytes, plus the upload limits (AC-45) - all overridable via <c>Vndr:PoFileStorage:*</c> config.</summary>
public sealed class PoFileStorageOptions
{
    public string RootPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "app-data", "po-files");

    public long MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;

    public int MaxFilesPerPo { get; init; } = 25;
}
