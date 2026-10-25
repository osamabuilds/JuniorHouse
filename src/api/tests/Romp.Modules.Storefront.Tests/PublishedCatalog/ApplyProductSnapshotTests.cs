using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Domain.PublishedCatalog;
using Romp.Modules.Storefront.Infrastructure.Persistence;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>
/// Published-catalog task 10: <see cref="ApplyProductSnapshotCommand"/> against a real PostgreSQL (Testcontainers, CLAUDE.md) - the
/// version-wins upsert (duplicate, out-of-order, newer), removal, kept stock and kept derivatives, and the derived flags.
/// </summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class ApplyProductSnapshotTests(StorefrontPostgres postgres)
{
    private static ApplyProductSnapshotCommand Apply(Romp.Modules.Catalog.Contracts.PublishedProductSnapshot? snapshot, long productId = 1) =>
        new(productId, snapshot);

    private static Task<PublishedProduct> ProductAsync(StfrDbContext db, long id = 1) =>
        db.Products.AsNoTracking().SingleAsync(p => p.Id == id);

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task Apply_NewSnapshot_StoresTheProductAndEveryRowItOwns()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        var outcome = await host.SendAsync(Apply(Snapshots.Product()));

        Assert.Equal(ProjectionOutcome.Created, outcome);
        await host.QueryAsync(async db =>
        {
            var product = await ProductAsync(db);
            Assert.Equal("Floral dress", product.Name);
            Assert.Equal("/girls/dresses/floral-1", product.Path);
            Assert.Equal(1, product.ContentVersion);
            Assert.Equal(1, await db.Variants.CountAsync());
            Assert.Equal(2, await db.Images.CountAsync());
            Assert.Equal(1, await db.Composition.CountAsync());
            Assert.Equal(1, await db.CareInstructions.CountAsync());
            Assert.Equal(1, await db.Collections.CountAsync());
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task Apply_NewSnapshot_MarksTheLowestSortImageAsMain_AndQueuesEveryWidthAndFormat()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await host.SendAsync(Apply(Snapshots.Product(images: [Snapshots.Image(12, 2, "images/b.jpg"), Snapshots.Image(11, 1)])));

        await host.QueryAsync(async db =>
        {
            Assert.Equal([11L], await db.Images.Where(i => i.IsMain).Select(i => i.Id).ToListAsync());
            var queued = await db.Derivatives.Where(d => d.ImageId == 11).ToListAsync();
            Assert.Equal(4, queued.Count); // two widths x WebP and JPEG
            Assert.All(queued, d => Assert.Equal(DerivativeStatus.Pending, d.Status));
            Assert.Equal(8, await db.Derivatives.CountAsync());
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Apply_SameSnapshotTwice_ChangesNothing()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(Apply(Snapshots.Product()));

        var outcome = await host.SendAsync(Apply(Snapshots.Product()));

        Assert.Equal(ProjectionOutcome.Unchanged, outcome);
        await host.QueryAsync(async db =>
        {
            Assert.Equal(1, await db.Products.CountAsync());
            Assert.Equal(1, await db.Variants.CountAsync());
            Assert.Equal(8, await db.Derivatives.CountAsync());
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Apply_OlderSnapshotAfterNewer_DoesNotRegressTheRow()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(Apply(Snapshots.Product(version: 3, name: "Newest name")));

        var outcome = await host.SendAsync(Apply(Snapshots.Product(version: 2, name: "Stale name")));

        Assert.Equal(ProjectionOutcome.Unchanged, outcome);
        await host.QueryAsync(async db =>
        {
            var product = await ProductAsync(db);
            Assert.Equal("Newest name", product.Name);
            Assert.Equal(3, product.ContentVersion);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-2")]
    public async Task Apply_NewerSnapshot_ReplacesContent_AndDropsVariantsAndImagesNoLongerPublished()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(Apply(Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A"), Snapshots.Variant(12, "SKU-B", size: 4)])));

        var outcome = await host.SendAsync(Apply(Snapshots.Product(
            version: 2, name: "Renamed", variants: [Snapshots.Variant(11, "SKU-A", price: 999m)], images: [Snapshots.Image(101, 1)])));

        Assert.Equal(ProjectionOutcome.Updated, outcome);
        await host.QueryAsync(async db =>
        {
            Assert.Equal("Renamed", (await ProductAsync(db)).Name);
            var variant = await db.Variants.AsNoTracking().SingleAsync();
            Assert.Equal(11, variant.Id);
            Assert.Equal(999m, variant.Price);
            Assert.Equal([101L], await db.Images.Select(i => i.Id).ToListAsync());
            Assert.Equal(4, await db.Derivatives.CountAsync()); // the removed image's queue went with it
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    public async Task Apply_StockHeldBeforeTheProductArrived_IsPickedUpBySku()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.QueryAsync(async db =>
        {
            db.Stock.Add(new StockRow(77, "SKU-A", 3, 4));
            await db.SaveChangesAsync();
            return 0;
        });

        await host.SendAsync(Apply(Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A")])));

        await host.QueryAsync(async db =>
        {
            var variant = await db.Variants.AsNoTracking().SingleAsync();
            Assert.Equal(3, variant.AvailableQuantity);
            Assert.Equal(4, variant.StockVersion);
            var product = await ProductAsync(db);
            Assert.True(product.IsAvailable);
            Assert.False(product.IsListable); // the main image is not ready yet (PCT-7)
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-9")]
    public async Task Apply_NoStockEverReceived_IsNotAvailable_AndTheVariantHasNoQuantity()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await host.SendAsync(Apply(Snapshots.Product()));

        await host.QueryAsync(async db =>
        {
            Assert.Null((await db.Variants.AsNoTracking().SingleAsync()).AvailableQuantity);
            var product = await ProductAsync(db);
            Assert.False(product.IsAvailable);
            Assert.Null(product.MinPrice); // a variant that is not yet available never counts toward the price (PriceSummary)
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    public async Task Apply_NewerSnapshot_KeepsTheStockAlreadyAppliedToAVariant()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.QueryAsync(async db =>
        {
            db.Stock.Add(new StockRow(77, "SKU-A", 9, 2));
            await db.SaveChangesAsync();
            return 0;
        });
        await host.SendAsync(Apply(Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A")])));

        await host.SendAsync(Apply(Snapshots.Product(version: 2, variants: [Snapshots.Variant(11, "SKU-A", price: 1100m)])));

        await host.QueryAsync(async db =>
        {
            var variant = await db.Variants.AsNoTracking().SingleAsync();
            Assert.Equal(9, variant.AvailableQuantity);
            Assert.Equal(1100m, variant.Price);
            Assert.Equal(1100m, (await ProductAsync(db)).MinPrice);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-4")]
    public async Task Apply_CompareAtAbovePrice_MarksTheProductOnSaleWithTheBiggestSaving()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.QueryAsync(async db =>
        {
            db.Stock.Add(new StockRow(77, "SKU-A", 20, 1));
            await db.SaveChangesAsync();
            return 0;
        });

        await host.SendAsync(Apply(Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A", price: 800m, compareAt: 1000m)])));

        await host.QueryAsync(async db =>
        {
            var product = await ProductAsync(db);
            Assert.True(product.IsOnSale);
            Assert.Equal(20m, product.MaxDiscountPercent);
            Assert.Equal(800m, product.MinPrice);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-7")]
    public async Task Apply_ChangedOriginal_StartsItsDerivativesOver_ButAnUnchangedOneKeepsThem()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(Apply(Snapshots.Product()));
        var firstIds = await host.QueryAsync(db => db.Derivatives.Where(d => d.ImageId == 101).Select(d => d.Id).ToListAsync());
        var otherIds = await host.QueryAsync(db => db.Derivatives.Where(d => d.ImageId == 102).Select(d => d.Id).ToListAsync());

        await host.SendAsync(Apply(Snapshots.Product(
            version: 2, images: [Snapshots.Image(101, 1, "images/new.jpg"), Snapshots.Image(102, 2, "images/b.jpg")])));

        await host.QueryAsync(async db =>
        {
            var firstNow = await db.Derivatives.Where(d => d.ImageId == 101).Select(d => d.Id).ToListAsync();
            var otherNow = await db.Derivatives.Where(d => d.ImageId == 102).Select(d => d.Id).ToListAsync();
            Assert.Equal(4, firstNow.Count);
            Assert.Empty(firstNow.Intersect(firstIds));
            Assert.Equal(otherIds.Order(), otherNow.Order());
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task Apply_Variants_TakeTheirSizeOrderFromTheSizeLookup()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await host.SendAsync(Apply(Snapshots.Product(variants: [Snapshots.Variant(11, "SKU-A", size: 4), Snapshots.Variant(12, "SKU-B", size: 9)])));

        await host.QueryAsync(async db =>
        {
            var seqs = await db.Variants.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.SizeSeq);
            Assert.Equal(20, seqs[11]);
            Assert.Equal(10, seqs[12]);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-2")]
    public async Task Apply_NullSnapshot_RemovesTheProductAndEveryRowItOwns()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(Apply(Snapshots.Product()));

        var outcome = await host.SendAsync(Apply(null));

        Assert.Equal(ProjectionOutcome.Removed, outcome);
        await host.QueryAsync(async db =>
        {
            Assert.Equal(0, await db.Products.CountAsync());
            Assert.Equal(0, await db.Variants.CountAsync());
            Assert.Equal(0, await db.Images.CountAsync());
            Assert.Equal(0, await db.Derivatives.CountAsync());
            Assert.Equal(0, await db.Composition.CountAsync());
            Assert.Equal(0, await db.CareInstructions.CountAsync());
            Assert.Equal(0, await db.Collections.CountAsync());
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-2")]
    public async Task Apply_NullSnapshotForAnUnknownProduct_IsAbsent()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        Assert.Equal(ProjectionOutcome.Absent, await host.SendAsync(Apply(null, productId: 99)));
    }

    [Fact]
    [Trait("Spec", "PCT-2")]
    public async Task Apply_RemovedThenPublishedAgain_ComesBackWithItsRows()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        await host.SendAsync(Apply(Snapshots.Product()));
        await host.SendAsync(Apply(null));

        var outcome = await host.SendAsync(Apply(Snapshots.Product(version: 2)));

        Assert.Equal(ProjectionOutcome.Created, outcome);
        await host.QueryAsync(async db =>
        {
            Assert.Equal(2, await db.Images.CountAsync());
            Assert.Equal(8, await db.Derivatives.CountAsync());
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-5")]
    public async Task Apply_SnapshotWithoutAVersion_IsRefused()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.SendAsync(Apply(Snapshots.Product(version: 0))));
        await host.QueryAsync(async db =>
        {
            Assert.Equal(0, await db.Products.CountAsync());
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-6")]
    public async Task Apply_SameVersionInARebuild_IsRecomputedAndStampedWithTheRun()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var runId = await host.QueryAsync(async db =>
        {
            var run = new RebuildRun(Snapshots.PublishedAt, "test");
            db.RebuildRuns.Add(run);
            await db.SaveChangesAsync();
            return run.Id;
        });
        await host.SendAsync(Apply(Snapshots.Product()));

        var outcome = await host.SendAsync(new ApplyProductSnapshotCommand(1, Snapshots.Product(), runId));

        Assert.Equal(ProjectionOutcome.Updated, outcome);
        await host.QueryAsync(async db =>
        {
            Assert.Equal(runId, (await ProductAsync(db)).RebuildRunId);
            return 0;
        });
    }

    [Fact]
    [Trait("Spec", "PCT-8")]
    public async Task Apply_ProductWithManyVariants_UsesAConstantNumberOfQueries()
    {
        using var host = await StorefrontHost.CreateAsync(postgres);
        var counter = new QueryCounter();
        var few = Snapshots.Product(id: 1, variants: [Snapshots.Variant(11, "A-1")]);
        var many = Snapshots.Product(id: 2, variants: [.. Enumerable.Range(1, 30).Select(i => Snapshots.Variant(200 + i, $"B-{i}"))]);

        await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var handler = new ApplyProductSnapshotCommandHandler(context, new FakeReadModelSettings(), new FakeCatalogLookups());
        await handler.Handle(Apply(few, 1), default);
        await context.SaveChangesAsync();
        var forFew = counter.Count;
        counter.Reset();
        await handler.Handle(Apply(many, 2), default);
        await context.SaveChangesAsync();

        Assert.True(forFew > 0);
        Assert.Equal(forFew, counter.Count);
    }
}
