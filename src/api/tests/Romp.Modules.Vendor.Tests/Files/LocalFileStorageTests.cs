using Romp.Modules.Vendor.Application;
using Romp.Modules.Vendor.Application.Files;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure;
using Romp.Modules.Vendor.Infrastructure.Files;

namespace Romp.Modules.Vendor.Tests.Files;

/// <summary>SCRUM-93 task 34 (AC-45).</summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "romp-po-file-storage-tests", Guid.NewGuid().ToString("N"));

    private LocalFileStorage CreateStorage() => new(new PoFileStorageOptions { RootPath = _rootPath });

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    [Fact]
    [Trait("Spec", "AC-45")]
    public async Task Save_GeneratesKeyNotDerivedFromFilename()
    {
        var storage = CreateStorage();

        var key1 = await storage.SaveAsync([1, 2, 3], CancellationToken.None);
        var key2 = await storage.SaveAsync([1, 2, 3], CancellationToken.None);

        Assert.True(Guid.TryParseExact(key1, "N", out _));
        Assert.NotEqual(key1, key2); // same content, different keys - nothing filename/content-derived
    }

    [Fact]
    [Trait("Spec", "AC-45")]
    public async Task Retrieve_ReturnsStoredBytes()
    {
        var storage = CreateStorage();
        byte[] content = [10, 20, 30, 40];

        var key = await storage.SaveAsync(content, CancellationToken.None);
        var retrieved = await storage.RetrieveAsync(key, CancellationToken.None);

        Assert.Equal(content, retrieved);
    }

    [Fact]
    [Trait("Spec", "AC-45")]
    public async Task Retrieve_KeyWithPathTraversal_Rejected()
    {
        var storage = CreateStorage();

        await Assert.ThrowsAsync<ArgumentException>(() => storage.RetrieveAsync("../../etc/passwd", CancellationToken.None));
    }
}
