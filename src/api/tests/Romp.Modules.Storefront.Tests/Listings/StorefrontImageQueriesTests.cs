using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Contracts;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>IStorefrontImageQueries (cart pictures): the smallest finished resize that is wide enough, WebP with its JPEG, in one call. Real PostgreSQL.</summary>
public sealed class StorefrontImageQueriesTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private static T Stamp<T>(T entity)
        where T : IAuditable
    {
        entity.InsrDte = Now;
        entity.InsrBy = "test";
        return entity;
    }

    private StfrDbContext NewContext() => new(new DbContextOptionsBuilder<StfrDbContext>()
        .UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "STFR"))
        .Options);

    private static DerivativeRow Done(long imageId, int width, DerivativeFormat format, string key)
    {
        var row = Stamp(new DerivativeRow(imageId, width, format));
        row.MarkDone(key);
        return row;
    }

    private async Task<StfrDbContext> SeedAsync()
    {
        var context = NewContext();
        await context.Database.MigrateAsync();
        context.Products.Add(Stamp(new PublishedProduct(
            1, 1, "/girls/dresses/floral-1", "Floral dress", "A dress.", "STY-1", "GIRLS", "Girls", "DRESSES", "Dresses", "KIDS", "Kids 4-8Y", Now, null)));
        await context.SaveChangesAsync();
        foreach (var id in new long[] { 10, 20, 30 })
        {
            context.Images.Add(Stamp(new ImageRow(id, 1, null, $"images/{id}.png", 800, 1000, "alt", (int)(id / 10), id == 10)));
        }

        await context.SaveChangesAsync();
        context.Derivatives.AddRange(
            Done(10, 160, DerivativeFormat.Webp, "a-160.webp"), Done(10, 320, DerivativeFormat.Webp, "a-320.webp"), Done(10, 320, DerivativeFormat.Jpeg, "a-320.jpg"),
            Done(10, 640, DerivativeFormat.Webp, "a-640.webp"), Done(10, 640, DerivativeFormat.Jpeg, "a-640.jpg"),
            Done(20, 160, DerivativeFormat.Webp, "b-160.webp"),
            Stamp(new DerivativeRow(30, 320, DerivativeFormat.Webp)));
        await context.SaveChangesAsync();
        return context;
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task TheSmallestResizeThatIsWideEnough_ComesBackWithItsJpegFallback()
    {
        await using var context = await SeedAsync();

        var thumbs = await new StorefrontImageQueries(context).GetThumbnailsAsync([10], 320, CancellationToken.None);

        var thumb = Assert.Single(thumbs);
        Assert.Equal(new ImageThumbnail(10, "/media/a-320.webp", "/media/a-320.jpg", 320), thumb);
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task WhenNothingIsWideEnough_TheWidestThereIsIsUsed_AndAMissingJpegIsNull()
    {
        await using var context = await SeedAsync();

        var thumb = Assert.Single(await new StorefrontImageQueries(context).GetThumbnailsAsync([20], 640, CancellationToken.None));

        Assert.Equal(new ImageThumbnail(20, "/media/b-160.webp", null, 160), thumb);
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task AnImageWithNoFinishedFile_IsAbsent_AndSoAreUnknownIds()
    {
        await using var context = await SeedAsync();

        var thumbs = await new StorefrontImageQueries(context).GetThumbnailsAsync([30, 999, -1, 0], 320, CancellationToken.None);

        Assert.Empty(thumbs);
    }

    [Fact]
    [Trait("Spec", "CRT-1")]
    public async Task ManyImages_AreOneCall_AndTheLimitIsEnforced()
    {
        await using var context = await SeedAsync();
        var queries = new StorefrontImageQueries(context);

        var both = await queries.GetThumbnailsAsync([10, 20], 160, CancellationToken.None);

        Assert.Equal([10L, 20L], both.Select(t => t.ImageId).Order());
        await Assert.ThrowsAsync<ArgumentException>(() => queries.GetThumbnailsAsync(Enumerable.Range(1, IStorefrontImageQueries.MaxIds + 1).Select(i => (long)i).ToList(), 160, CancellationToken.None));
    }
}
