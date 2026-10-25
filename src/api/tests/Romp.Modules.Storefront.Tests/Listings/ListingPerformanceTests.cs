using System.Diagnostics;
using Npgsql;
using Romp.Modules.Storefront.Application.Listings;
using Romp.Modules.Storefront.Domain.Listings;
using Romp.Modules.Storefront.Infrastructure;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Listings;

/// <summary>Listings tasks 7 and 8: a page of a 1,000-product, 8-variant catalogue is a constant number of queries and fast (LST-13, NFR-02).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class ListingPerformanceTests(StorefrontPostgres postgres)
{
    private const int Products = 1_000;
    private const int Offset = 1_000_000;

    private static readonly SeedVariant[] EightVariants =
    [
        new("RED", "3-4Y", 700m, 5), new("RED", "5-6Y", 800m, 5), new("RED", "7-8Y", 900m, 5), new("RED", "9-10Y", 1000m, 5),
        new("NAVY", "3-4Y", 750m, 5), new("NAVY", "5-6Y", 850m, 5), new("NAVY", "7-8Y", 950m, 5), new("NAVY", "9-10Y", 1050m, 0),
    ];

    /// <summary>Seeds one product through the real projection, then clones its rows 999 times in SQL, so the catalogue is built in a moment.</summary>
    private static async Task<StorefrontHost> HostWithThousandProductsAsync(StorefrontPostgres postgres)
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", EightVariants, name: "Seed");

        await using var connection = new NpgsqlConnection(host.ConnectionString);
        await connection.OpenAsync();
        await CloneAsync(connection, "PRDT_MAIN", shift: ["ID"], suffix: ["PATH"]);
        await CloneAsync(connection, "PRDT_VRNT", shift: ["ID", "PRDT_ID"], suffix: ["SKU_CODE", "SKU_NORM"]);
        await CloneAsync(connection, "PRDT_IMG", shift: ["ID", "PRDT_ID"], suffix: []);
        await CloneAsync(connection, "IMG_DRV", shift: ["IMG_ID"], suffix: [], skip: ["ID"]);
        await using var analyze = new NpgsqlCommand("ANALYZE", connection);
        await analyze.ExecuteNonQueryAsync();
        return host;
    }

    private static async Task CloneAsync(NpgsqlConnection connection, string table, string[] shift, string[] suffix, string[]? skip = null)
    {
        var columns = new List<string>();
        await using (var read = new NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_schema = 'STFR' AND table_name = @t AND is_generated = 'NEVER' ORDER BY ordinal_position", connection))
        {
            read.Parameters.AddWithValue("t", table);
            await using var reader = await read.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }
        }

        columns.RemoveAll(c => skip?.Contains(c) == true);
        var list = string.Join(", ", columns.Select(c => $"\"{c}\""));
        var select = string.Join(", ", columns.Select(c =>
            shift.Contains(c) ? $"t.\"{c}\" + n * {Offset}"
            : suffix.Contains(c) ? $"t.\"{c}\" || '-' || n"
            : $"t.\"{c}\""));
        var sql = $"INSERT INTO \"STFR\".\"{table}\" ({list}) SELECT {select} FROM \"STFR\".\"{table}\" t CROSS JOIN generate_series(1, {Products - 1}) n";
#pragma warning disable CA2100 // identifiers come from information_schema and this file's own constants, never from input
        await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
    }

    private static ListingFilter Filter(string? size = null, string? colour = null, string? price = null, string? sort = null) =>
        ListingFilter.Parse(size is null ? null : [size], colour is null ? null : [colour], null, price, sort, null, null).Filter!;

    [Fact]
    [Trait("Spec", "LST-13")]
    public async Task APageOfAThousandProducts_IsConstantQueries_AndUnder200Milliseconds()
    {
        using var host = await HostWithThousandProductsAsync(postgres);
        Assert.Equal(Products, await host.QueryAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Products)));
        Assert.Equal(Products * 8, await host.QueryAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Variants)));

        var counter = new QueryCounter();
        await using var context = StorefrontPostgres.NewContext(host.ConnectionString, counter);
        var handler = new SearchListingQueryHandler(context, host.Lookups, new CatalogLookupCache(TimeProvider.System), new FakeListingSettings(), TimeProvider.System);
        var scope = ListingScope.Audience("girls");

        var cases = new[]
        {
            Filter(),
            Filter(sort: "price-asc"),
            Filter(size: "5-6y", colour: "navy", price: "700-900"),
            Filter(size: "9-10y", sort: "price-desc"),
        };

        // One warm-up so connection set-up and query compilation are not counted; then the best of three per case.
        await handler.Handle(new SearchListingQuery(scope, cases[0]), default);
        var counts = new List<int>();
        foreach (var filter in cases)
        {
            var best = TimeSpan.MaxValue;
            for (var i = 0; i < 3; i++)
            {
                counter.Reset();
                var clock = Stopwatch.StartNew();
                var page = await handler.Handle(new SearchListingQuery(scope, filter), default);
                clock.Stop();
                best = clock.Elapsed < best ? clock.Elapsed : best;
                counts.Add(counter.Count);
                Assert.Equal(ListingOutcome.Found, page.Outcome);
                Assert.Equal(24, page.Value!.Tiles.Count);
            }

            Assert.True(best < TimeSpan.FromMilliseconds(200), $"A page took {best.TotalMilliseconds:F0} ms for {filter.ToCanonicalQuery()}.");
        }

        // Count, page, images, derivatives, sizes: the same five queries for every case, however many products and variants.
        Assert.All(counts, c => Assert.Equal(5, c));
    }

    [Fact]
    [Trait("Spec", "LST-13")]
    public async Task ThePriceSort_PlanUsesTheProductIndex_NotAScanOfEveryVariantPerProduct()
    {
        using var host = await HostWithThousandProductsAsync(postgres);
        await using var connection = new NpgsqlConnection(host.ConnectionString);
        await connection.OpenAsync();

        await using var explain = new NpgsqlCommand(
            "EXPLAIN SELECT v.\"PRDT_ID\" FROM \"STFR\".\"PRDT_VRNT\" v WHERE v.\"PRDT_ID\" = 5 AND v.\"SELL_IND\" AND v.\"AVLB_QTY\" > 0", connection);
        var plan = new List<string>();
        await using (var reader = await explain.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                plan.Add(reader.GetString(0));
            }
        }

        Assert.Contains(plan, line => line.Contains("IX_PRDT_VRNT", StringComparison.Ordinal));
        Assert.DoesNotContain(plan, line => line.Contains("Seq Scan", StringComparison.Ordinal));
    }
}
