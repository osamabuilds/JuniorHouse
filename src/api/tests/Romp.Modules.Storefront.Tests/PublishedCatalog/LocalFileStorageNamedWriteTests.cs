using Romp.BuildingBlocks.Storage;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog task 17: the named write the image worker uses is atomic, replaces, and refuses anything but a bare file name.</summary>
public sealed class LocalFileStorageNamedWriteTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "romp-storage-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private LocalFileStorage Storage() => new(new FileStorageOptions { RootPath = _root });

    [Fact]
    [Trait("Spec", "PCT-7")]
    public async Task SaveNamed_WritesUnderTheGivenKey_ReplacesIt_AndLeavesNoTemporaryFile()
    {
        var storage = Storage();

        await storage.SaveNamedAsync("abc-320.webp", [1, 2, 3], default);
        await storage.SaveNamedAsync("abc-320.webp", [9], default);

        Assert.Equal([9], await storage.RetrieveAsync("abc-320.webp", default));
        Assert.Equal(["abc-320.webp"], Directory.GetFiles(_root).Select(Path.GetFileName).ToList());
    }

    [Theory]
    [Trait("Spec", "PCT-7")]
    [InlineData("../escape")]
    [InlineData("a/b.webp")]
    [InlineData("a\\b.webp")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData(".hidden")]
    public async Task SaveNamed_KeyThatIsNotABareFileName_IsRefused(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Storage().SaveNamedAsync(key, [1], default));
    }
}
