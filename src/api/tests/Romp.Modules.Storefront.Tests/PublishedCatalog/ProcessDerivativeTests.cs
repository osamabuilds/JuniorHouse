using System.Text;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>
/// Published-catalog task 17: <see cref="ProcessDerivativeCommand"/> against a real PostgreSQL with a fake resizer and in-memory storage -
/// claiming, hashed names, retries with backoff, permanent rejection and the image-ready flag (PCT-7, PCT-10).
/// </summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class ProcessDerivativeTests(StorefrontPostgres postgres)
{
    private static readonly byte[] Original = Encoding.ASCII.GetBytes("original-bytes");

    private static async Task<StorefrontHost> HostWithProductAsync(StorefrontPostgres postgres)
    {
        var host = await StorefrontHost.CreateAsync(postgres);
        host.Storage.Files["images/a.jpg"] = Original;
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(
            variants: [Snapshots.Variant(11, "SKU-A")], images: [Snapshots.Image(101, 1)])));
        return host;
    }

    private static async Task DrainAsync(StorefrontHost host)
    {
        while (await host.SendAsync(new ProcessDerivativeCommand()) != DerivativeOutcome.NothingDue)
        {
        }
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public async Task Process_PendingDerivatives_WritesEachUnderItsHashedName_AndMarksTheProductImageReady()
    {
        using var host = await HostWithProductAsync(postgres);

        await DrainAsync(host);

        var hash = DerivativeName.HashOf(Original);
        Assert.Equal(
            [$"{hash}-320.jpg", $"{hash}-320.webp", $"{hash}-640.jpg", $"{hash}-640.webp"],
            host.Storage.Files.Keys.Where(k => k != "images/a.jpg").Order().ToList());
        await host.QueryAsync(async db =>
        {
            var rows = await db.Derivatives.AsNoTracking().Where(d => d.ImageId == 101).ToListAsync();
            Assert.All(rows, d => Assert.Equal(DerivativeStatus.Done, d.Status));
            Assert.All(rows, d => Assert.Equal(DerivativeName.For(hash, d.Width, d.Format), d.FileKey));
            Assert.Equal(hash, (await db.Images.AsNoTracking().SingleAsync(i => i.Id == 101)).ContentHash);
            Assert.True((await db.Products.AsNoTracking().SingleAsync()).ImageReady);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public async Task Process_NothingPending_ReportsNothingDue()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        Assert.Equal(DerivativeOutcome.NothingDue, await host.SendAsync(new ProcessDerivativeCommand()));
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public async Task Process_ImageAboveThePixelCap_FailsTheRowForGood_WithTheRejectionCode()
    {
        using var host = await HostWithProductAsync(postgres);
        host.Resizer.Rejection = new ImageRejectedException("TOO_LARGE", "too many pixels");

        var outcome = await host.SendAsync(new ProcessDerivativeCommand());

        Assert.Equal(DerivativeOutcome.Failed, outcome);
        await host.QueryAsync(async db =>
        {
            var failed = await db.Derivatives.AsNoTracking().SingleAsync(d => d.Status == DerivativeStatus.Failed);
            Assert.Equal("TOO_LARGE", failed.ErrorCode);
            Assert.Equal(1, failed.AttemptCount);
            Assert.Null(failed.NextAttemptAt);
            Assert.False((await db.Products.AsNoTracking().SingleAsync()).ImageReady);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public async Task Process_StorageWriteFails_RetriesWithBackoff_ThenFailsAfterTheLastAttempt()
    {
        using var host = await HostWithProductAsync(postgres);
        host.Storage.WriteFailure = new IOException("disk full");

        Assert.Equal(DerivativeOutcome.WillRetry, await host.SendAsync(new ProcessDerivativeCommand()));
        await host.QueryAsync(async db =>
        {
            var row = await db.Derivatives.AsNoTracking().SingleAsync(d => d.AttemptCount == 1);
            Assert.Equal(DerivativeStatus.Pending, row.Status);
            Assert.Equal("STORAGE_ERROR", row.ErrorCode);
            Assert.True(row.NextAttemptAt > DateTimeOffset.UtcNow); // not due again yet
            return 0;
        });

        // Make the row due, fail it up to the allowed attempts (3 in the test settings).
        await host.QueryAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE \"STFR\".\"IMG_DRV\" SET \"NXT_ATMP_DTE\" = now() - interval '1 minute' WHERE \"ATMP_QTY\" = 1");
            return 0;
        });
        Assert.Equal(DerivativeOutcome.WillRetry, await host.SendAsync(new ProcessDerivativeCommand())); // second attempt of the same row
        await host.QueryAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE \"STFR\".\"IMG_DRV\" SET \"NXT_ATMP_DTE\" = now() - interval '1 minute' WHERE \"ATMP_QTY\" = 2");
            return 0;
        });
        Assert.Equal(DerivativeOutcome.Failed, await host.SendAsync(new ProcessDerivativeCommand())); // third and last

        await host.QueryAsync(async db =>
        {
            var failed = await db.Derivatives.AsNoTracking().SingleAsync(d => d.Status == DerivativeStatus.Failed);
            Assert.Equal(3, failed.AttemptCount);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public async Task Process_OriginalMissingFromStorage_RecordsOriginalMissing()
    {
        using var host = await HostWithProductAsync(postgres);
        host.Storage.Files.Clear();

        Assert.Equal(DerivativeOutcome.WillRetry, await host.SendAsync(new ProcessDerivativeCommand()));

        await host.QueryAsync(async db =>
        {
            Assert.Equal("ORIGINAL_MISSING", (await db.Derivatives.AsNoTracking().SingleAsync(d => d.AttemptCount == 1)).ErrorCode);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-10")]
    public async Task Process_ChangedOriginal_GetsNewHashedNames_AndLeavesTheOldFilesAlone()
    {
        using var host = await HostWithProductAsync(postgres);
        await DrainAsync(host);
        var oldHash = DerivativeName.HashOf(Original);

        var replacement = Encoding.ASCII.GetBytes("a different picture");
        host.Storage.Files["images/b.jpg"] = replacement;
        await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(
            version: 2, variants: [Snapshots.Variant(11, "SKU-A")], images: [Snapshots.Image(101, 1, "images/b.jpg")])));
        await DrainAsync(host);

        var newHash = DerivativeName.HashOf(replacement);
        Assert.NotEqual(oldHash, newHash);
        Assert.Contains($"{newHash}-320.webp", host.Storage.Files.Keys);
        Assert.Contains($"{oldHash}-320.webp", host.Storage.Files.Keys); // removed later by the sweep (task 18)
        await host.QueryAsync(async db =>
        {
            Assert.All(await db.Derivatives.AsNoTracking().Where(d => d.ImageId == 101).ToListAsync(),
                d => Assert.Equal(DerivativeName.For(newHash, d.Width, d.Format), d.FileKey));
            return 0;
        });
    }
}
