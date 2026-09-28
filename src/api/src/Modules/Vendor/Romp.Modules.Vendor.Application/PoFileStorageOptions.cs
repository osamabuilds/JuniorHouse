namespace Romp.Modules.Vendor.Application;

/// <summary>SCRUM-93 task 34. Where the local-disk <see cref="IFileStorage"/> implementation (Infrastructure) writes PO file bytes - overridable via <c>Vndr:PoFileStorage:RootPath</c> config.</summary>
public sealed class PoFileStorageOptions
{
    public string RootPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "app-data", "po-files");
}
