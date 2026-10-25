using System.Diagnostics;
using Romp.Modules.Catalog.Contracts;
using Romp.Modules.Storefront.Application.ProductPages;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Tests.Support;
using Romp.Modules.Warehouse.Contracts;

namespace Romp.Modules.Storefront.Tests.ProductPages;

/// <summary>Product pages task 9 (and published-catalog task 23): a page costs the same few queries however many variants, images and lines the product has (PDP-12, PCT-8).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class ProductPagePerformanceTests(StorefrontPostgres postgres)
{
    private static readonly (short Id, string Code)[] Sizes = [(9, "5-6Y"), (4, "7-8Y"), (7, "3-4Y"), (6, "9-10Y")];

    private static async Task PublishAsync(StorefrontHost host, long id, int colours, int images, int lines)
    {
        host.Storage.Files["images/a.jpg"] = [1, 2, 3];
        var variants = new List<PublishedVariant>();
        for (var c = 1; c <= colours; c++)
        {
            foreach (var (sizeId, size) in Sizes)
            {
                var variantId = id * 1000 + c * 10 + sizeId;
                variants.Add(new PublishedVariant(
                    variantId, new PublishedCode((short)c, $"C{c}", $"Colour {c}"), new PublishedCode(sizeId, size, size), $"SKU-{variantId}", 1000m + sizeId, null, true));
            }
        }

        var pictures = Enumerable.Range(1, images).Select(i => new PublishedImage(id * 100 + i, i % 2 == 0 ? (short?)null : (short)1, "images/a.jpg", 1200, 1600, $"Picture {i}", i)).ToList();
        var snapshot = Snapshots.Product(id: id, path: $"/girls/dresses/p-{id}", variants: variants, images: pictures) with
        {
            Composition = [.. Enumerable.Range(1, lines).Select(i => new PublishedCompositionLine(new PublishedCode((short)i, $"F{i}", $"Fibre {i}"), 100m / lines))],
            Care = [.. Enumerable.Range(1, lines).Select(i => new PublishedCareInstruction(new PublishedCode((short)i, $"K{i}", $"Care {i}"), $"sym-{i}"))],
        };
        await host.SendAsync(new ApplyProductSnapshotCommand(id, snapshot));
        foreach (var v in variants)
        {
            await host.SendAsync(new ApplyAvailabilityCommand(new StockAvailability(v.VariantId, v.SkuCode!, 5, 1)));
        }

        while (await host.SendAsync(new ProcessDerivativeCommand()) != DerivativeOutcome.NothingDue)
        {
        }

        host.Paths.Answers[$"/girls/dresses/p-{id}"] = new PathResolutionResult(PathOutcome.Current, id, $"/girls/dresses/p-{id}");
    }

    [Fact]
    [Trait("Spec", "PCT-8")]
    [Trait("Spec", "PDP-12")]
    public async Task APage_IsTheSameNumberOfQueries_ForOneVariantAndForEightWithTenImages_AndFast()
    {
        using var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await PublishAsync(host, 1, colours: 1, images: 1, lines: 1);
        await PublishAsync(host, 2, colours: 2, images: 10, lines: 6);

        var counter = new QueryCounter();
        await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var handler = new GetProductPageQueryHandler(context, host.Paths, new FakeReadModelSettings(), host.PageMetrics);
        await handler.Handle(new GetProductPageQuery("/girls/dresses/p-1", null, null), default); // warm-up: connection and plans

        var counts = new List<int>();
        foreach (var id in new[] { 1, 2 })
        {
            counter.Reset();
            var clock = Stopwatch.StartNew();
            var result = await handler.Handle(new GetProductPageQuery($"/girls/dresses/p-{id}", null, null), default);
            clock.Stop();

            Assert.Equal("Found", result.Outcome);
            Assert.True(clock.ElapsedMilliseconds < 200, $"page {id} took {clock.ElapsedMilliseconds} ms");
            counts.Add(counter.Count);
        }

        Assert.Equal(counts[0], counts[1]);
        Assert.InRange(counts[0], 1, 8);
    }
}
