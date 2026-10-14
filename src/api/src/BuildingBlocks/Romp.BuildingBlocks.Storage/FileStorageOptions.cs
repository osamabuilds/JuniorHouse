namespace Romp.BuildingBlocks.Storage;

/// <summary>Where <see cref="LocalFileStorage"/> writes bytes. A module adds its own limits by deriving (e.g. Vendor's PoFileStorageOptions).</summary>
public class FileStorageOptions
{
    public string RootPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "app-data", "po-files");
}
