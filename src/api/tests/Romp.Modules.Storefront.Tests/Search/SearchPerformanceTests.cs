using System.Diagnostics;
using Romp.Modules.Storefront.Application.Search;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Infrastructure;
using Romp.Modules.Storefront.Infrastructure.Search;
using Romp.Modules.Storefront.Tests.Listings;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Search;

/// <summary>Search task 9: search and autosuggest over 1,000 products of 8 variants are a constant number of queries and fast (SRC-4, 7, NFR-02).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class SearchPerformanceTests(StorefrontPostgres postgres)
{
    private static ListingFilter Filter(string? size = null, string? sort = null, string? page = null) =>
        ListingFilter.Parse(size is null ? null : [size], null, null, null, sort, page, null).Filter!;

    private static TimeSpan Best(Stopwatch[] runs) => runs.Min(r => r.Elapsed);

    [Fact]
    [Trait("Spec", "SRC-7")]
    public async Task ASearchOfAThousandProducts_IsConstantQueries_AndUnderOneSecond()
    {
        using var host = await ListingPerformanceTests.HostWithThousandProductsAsync(postgres, "Denim dungaree");
        var counter = new QueryCounter();
        await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var handler = new SearchProductsQueryHandler(
            context, new ProductSearchIndex(context), new SynonymCache(TimeProvider.System), new FakeSearchSettings(),
            host.Lookups, new CatalogLookupCache(TimeProvider.System));

        var cases = new[]
        {
            ("dungaree", Filter()),
            ("dungaree", Filter(sort: "price-asc")),
            ("dungaree", Filter(size: "5-6y", sort: "price-desc")),
            ("dungaree", Filter(page: "2")),
            ("romper", Filter()), // through a synonym
        };

        await handler.Handle(new SearchProductsQuery("dungaree", Filter()), default); // warm-up: connection, compiled queries, synonym cache
        var counts = new List<int>();
        foreach (var (text, filter) in cases)
        {
            var best = TimeSpan.MaxValue;
            for (var i = 0; i < 3; i++)
            {
                counter.Reset();
                var clock = Stopwatch.StartNew();
                var result = await handler.Handle(new SearchProductsQuery(text, filter), default);
                clock.Stop();
                best = clock.Elapsed < best ? clock.Elapsed : best;
                counts.Add(counter.Count);
                Assert.Equal(SearchOutcome.Found, result.Outcome);
                Assert.Equal(24, result.Page.Tiles.Count);
            }

            Assert.True(best < TimeSpan.FromSeconds(1), $"A search took {best.TotalMilliseconds:F0} ms for '{text}' {filter.ToCanonicalQuery()}.");
        }

        // The same queries for every case, however many products match: index, rows, pictures, derivatives, sizes, four for the options.
        Assert.All(counts, c => Assert.Equal(counts[0], c));
        Assert.True(counts[0] <= 12, $"A search ran {counts[0]} queries.");
    }

    [Fact]
    [Trait("Spec", "SRC-7")]
    public async Task AutosuggestOverAThousandProducts_IsConstantQueries_AndUnder300Milliseconds()
    {
        using var host = await ListingPerformanceTests.HostWithThousandProductsAsync(postgres, "Denim dungaree");
        var counter = new QueryCounter();
        await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var synonyms = new SynonymCache(TimeProvider.System);

        SuggestQueryHandler NewHandler() => new(
            context, new ProductSearchIndex(context), synonyms, new FakeSearchSettings(), new SuggestCache(), host.Lookups, new CatalogLookupCache(TimeProvider.System));

        await NewHandler().Handle(new SuggestQuery("dun"), default);
        var counts = new List<int>();
        foreach (var text in new[] { "dun", "dungar", "denim dung", "dre" })
        {
            var best = TimeSpan.MaxValue;
            for (var i = 0; i < 3; i++)
            {
                counter.Reset();
                var clock = Stopwatch.StartNew();
                var result = await NewHandler().Handle(new SuggestQuery(text), default); // a fresh cache each time: the real work
                clock.Stop();
                best = clock.Elapsed < best ? clock.Elapsed : best;
                counts.Add(counter.Count);
                Assert.True(result.Items.Count <= 8);
            }

            Assert.True(best < TimeSpan.FromMilliseconds(300), $"A suggestion took {best.TotalMilliseconds:F0} ms for '{text}'.");
        }

        Assert.True(counts.Max() <= 6, $"Autosuggest ran up to {counts.Max()} queries.");
    }

    [Fact]
    [Trait("Spec", "SRC-4")]
    public async Task AnExactSkuAmongAThousandProducts_IsOneIndexedRead()
    {
        using var host = await ListingPerformanceTests.HostWithThousandProductsAsync(postgres, "Denim dungaree");
        var counter = new QueryCounter();
        await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var handler = new LookupSkuQueryHandler(context);
        await handler.Handle(new LookupSkuQuery("sku-1-0"), default);

        counter.Reset();
        var clock = Stopwatch.StartNew();
        var target = await handler.Handle(new LookupSkuQuery("SKU-1-0-500"), default);
        clock.Stop();

        Assert.NotNull(target);
        Assert.Equal(1, counter.Count);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(100), $"A SKU lookup took {clock.Elapsed.TotalMilliseconds:F0} ms.");
    }
}
