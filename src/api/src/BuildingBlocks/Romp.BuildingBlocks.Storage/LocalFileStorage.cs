

namespace Romp.BuildingBlocks.Storage;

/// <summary>
/// SCRUM-93 task 34 (AC-45). Dev/Docker Compose implementation of <see cref="IFileStorage"/> - one
/// file per generated (GUID) key under <see cref="FileStorageOptions.RootPath"/>, created on first
/// use. The key never encodes or derives from the caller's filename.
/// </summary>
public sealed class LocalFileStorage(FileStorageOptions options) : IFileStorage
{
    private static readonly System.Text.RegularExpressions.Regex BareName =
        new("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

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

    public async Task SaveNamedAsync(string storageKey, byte[] content, CancellationToken cancellationToken)
    {
        if (!BareName.IsMatch(storageKey) || storageKey.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Storage key is not a valid bare file name.", nameof(storageKey));
        }

        Directory.CreateDirectory(options.RootPath);
        var temporary = Path.Combine(options.RootPath, $"{storageKey}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, content, cancellationToken);
            File.Move(temporary, Path.Combine(options.RootPath, storageKey), overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (storageKey.Contains('/') || storageKey.Contains('\\') || storageKey.Contains(".."))
        {
            throw new ArgumentException("Storage key is not a valid generated key.", nameof(storageKey));
        }

        var path = Path.Combine(options.RootPath, storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
