using System.Text;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog task 18: <see cref="SweepDerivativesCommand"/> removes old unreferenced derivative files and nothing else (PCT-10).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SweepDerivativesTests(StorefrontPostgres postgres)
{
    private static readonly byte[] Original = Encoding.ASCII.GetBytes("original-bytes");

    private static readonly string OldHash = DerivativeName.HashOf(Encoding.ASCII.GetBytes("an earlier picture"));

    private static async Task<StorefrontHost> HostWithDerivativesAsync(StorefrontPostgres postgres)
    {
        var host = await StorefrontHost.CreateAsync(postgres);
        host.Storage.Files["images/a.jpg"] = Original;
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(
            variants: [Snapshots.Variant(11, "SKU-A")], images: [Snapshots.Image(101, 1)])));
        while (await host.SendAsync(new ProcessDerivativeCommand()) != DerivativeOutcome.NothingDue)
        {
        }

        return host;
    }

    private static void AddOldFile(StorefrontHost host, string key, int ageDays)
    {
        host.Storage.Files[key] = [1];
        host.Storage.Modified[key] = DateTimeOffset.UtcNow.AddDays(-ageDays);
    }

    [Fact]
    [Trait("Spec", "PCT-10")]
    public async Task Sweep_UnreferencedFileOlderThanTheLifetime_IsRemoved_ReferencedAndYoungOnesStay()
    {
        using var host = await HostWithDerivativesAsync(postgres);
        var liveHash = DerivativeName.HashOf(Original);
        AddOldFile(host, $"{OldHash}-320.webp", ageDays: 40);
        AddOldFile(host, $"{OldHash}-640.webp", ageDays: 2);
        AddOldFile(host, $"{liveHash}-320.webp", ageDays: 400); // referenced: kept however old
        AddOldFile(host, "0123456789abcdef0123456789abcdef", ageDays: 400); // an original or other file: never touched

        var removed = await host.SendAsync(new SweepDerivativesCommand());

        Assert.Equal(1, removed);
        Assert.DoesNotContain($"{OldHash}-320.webp", host.Storage.Files.Keys);
        Assert.Contains($"{OldHash}-640.webp", host.Storage.Files.Keys);
        Assert.Contains($"{liveHash}-320.webp", host.Storage.Files.Keys);
        Assert.Contains("0123456789abcdef0123456789abcdef", host.Storage.Files.Keys);
        Assert.Contains("images/a.jpg", host.Storage.Files.Keys);
    }

    [Fact]
    [Trait("Spec", "PCT-10")]
    public async Task Sweep_NothingOldEnough_RemovesNothing()
    {
        using var host = await HostWithDerivativesAsync(postgres);

        Assert.Equal(0, await host.SendAsync(new SweepDerivativesCommand()));
    }
}
