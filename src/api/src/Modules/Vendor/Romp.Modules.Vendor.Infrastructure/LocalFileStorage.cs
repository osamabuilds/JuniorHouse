using Romp.Modules.Vendor.Application;

namespace Romp.Modules.Vendor.Infrastructure;

/// <summary>
/// SCRUM-93 task 34 (AC-45). Dev/Docker Compose implementation of <see cref="IFileStorage"/> - one
/// file per generated (GUID) key under <see cref="PoFileStorageOptions.RootPath"/>, created on first
/// use. The key never encodes or derives from the caller's filename.
/// </summary>
public sealed class LocalFileStorage(PoFileStorageOptions options) : IFileStorage
{
    public async Task<string> SaveAsync(byte[] content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.RootPath);
        var storageKey = Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(Path.Combine(options.RootPath, storageKey), content, cancellationToken);
        return storageKey;
    }

    public Task<byte[]> RetrieveAsync(string storageKey, CancellationToken cancellationToken)
    {
        // Defence in depth: SaveAsync only ever generates a bare GUID, but this rejects any
        // caller-supplied key that could otherwise escape RootPath (e.g. "../../secrets").
        if (storageKey.Contains('/') || storageKey.Contains('\\') || storageKey.Contains(".."))
        {
            throw new ArgumentException("Storage key is not a valid generated key.", nameof(storageKey));
        }

        return File.ReadAllBytesAsync(Path.Combine(options.RootPath, storageKey), cancellationToken);
    }
}
