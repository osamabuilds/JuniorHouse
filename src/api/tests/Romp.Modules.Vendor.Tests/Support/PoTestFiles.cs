using Romp.Modules.Vendor.Application.Files;

namespace Romp.Modules.Vendor.Tests.Support;

/// <summary>Keeps stored bytes in memory; the count lets a test prove nothing was stored for a rejected upload.</summary>
internal sealed class InMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = [];

    public int Count => _files.Count;

    public Task<string> SaveAsync(byte[] content, CancellationToken cancellationToken)
    {
        var key = Guid.NewGuid().ToString("N");
        _files[key] = content;
        return Task.FromResult(key);
    }

    public Task<byte[]> RetrieveAsync(string storageKey, CancellationToken cancellationToken) => Task.FromResult(_files[storageKey]);
}

internal static class PoTestFiles
{
    public const short TechPack = 1;
    public const short CostSheet = 6;

    public static byte[] Pdf(string body = "x") => System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 " + body);
}
