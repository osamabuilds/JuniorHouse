using Romp.BuildingBlocks.Storage;

namespace Romp.Modules.Storefront.Tests.Support;

/// <summary>An <see cref="IFileStorage"/> held in a dictionary, with switches to make reads or writes fail, so image-worker tests need no disk.</summary>
internal sealed class InMemoryFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    /// <summary>When set, the next writes throw this.</summary>
    public IOException? WriteFailure { get; set; }

    public Task<string> SaveAsync(byte[] content, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString("N");
        Files[key] = content;
        return Task.FromResult(key);
    }

    public Task<byte[]> RetrieveAsync(string storageKey, CancellationToken cancellationToken) =>
        Files.TryGetValue(storageKey, out var content) ? Task.FromResult(content) : throw new FileNotFoundException(storageKey);

    public Task SaveNamedAsync(string storageKey, byte[] content, CancellationToken cancellationToken)
    {
        if (WriteFailure is not null)
        {
            throw WriteFailure;
        }

        Files[storageKey] = content;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }
}
