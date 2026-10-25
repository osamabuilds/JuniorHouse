using Microsoft.EntityFrameworkCore;
using Romp.Modules.Storefront.Application.PublishedCatalog;
using Romp.Modules.Storefront.Application.Search;
using Romp.Modules.Storefront.Tests.Support;

namespace Romp.Modules.Storefront.Tests.Search;

/// <summary>Search task 5: <see cref="LookupSkuQuery"/> against a real PostgreSQL (SRC-4, 5, 6, 14).</summary>
[Collection(StorefrontPostgresFixtureDefinition.Name)]
public sealed class LookupSkuTests(StorefrontPostgres postgres)
{
    // ListingSeeder names the SKU of variant i of product n "SKU-n-i" and gives every product the style code "STY-1".
    private async Task<StorefrontHost> HostWithOneProductAsync(int stock = 10)
    {
        var host = await ListingSeeder.HostWithLookupsAsync(postgres);
        await ListingSeeder.ListableAsync(host, 1, "GIRLS", "DRESSES", [new SeedVariant("NAVY", "5-6Y", 800m, stock)], name: "Floral");
        return host;
    }

    [Theory]
    [Trait("Spec", "SRC-4")]
    [Trait("Spec", "SRC-5")]
    [InlineData("SKU-1-0")]
    [InlineData("sku-1-0")]
    [InlineData("  Sku-1-0  ")]
    public async Task Lookup_ExactSku_OpensTheProductWithColourAndSizeSelected(string code)
    {
        using var host = await HostWithOneProductAsync();

        var target = await host.SendAsync(new LookupSkuQuery(code));

        Assert.Equal(new SkuTargetDto("/girls/dresses/p-1", "navy", "5-6y"), target);
    }

    [Theory]
    [Trait("Spec", "SRC-4")]
    [InlineData("STY-1")]
    [InlineData("sty-1")]
    public async Task Lookup_StyleCode_OpensTheProduct(string code)
    {
        using var host = await HostWithOneProductAsync();

        var target = await host.SendAsync(new LookupSkuQuery(code));

        Assert.Equal(new SkuTargetDto("/girls/dresses/p-1", null, null), target);
    }

    [Theory]
    [Trait("Spec", "SRC-3")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nothing-here")]
    [InlineData("' OR 1=1 --")]
    [InlineData("sku-1")]
    public async Task Lookup_UnknownOrHostileText_FindsNothing(string? code)
    {
        using var host = await HostWithOneProductAsync();

        Assert.Null(await host.SendAsync(new LookupSkuQuery(code)));
    }

    [Fact]
    [Trait("Spec", "SRC-6")]
    public async Task Lookup_SkuOfAProductThatIsNoLongerPublished_FindsNothing()
    {
        using var host = await HostWithOneProductAsync();
        await host.SendAsync(new ApplyProductSnapshotCommand(1, null));

        Assert.Null(await host.SendAsync(new LookupSkuQuery("SKU-1-0")));
        Assert.Null(await host.SendAsync(new LookupSkuQuery("STY-1")));
    }

    [Fact]
    [Trait("Spec", "SRC-14")]
    public async Task Lookup_SoldOutProduct_IsStillFound()
    {
        using var host = await HostWithOneProductAsync(stock: 0);

        Assert.NotNull(await host.SendAsync(new LookupSkuQuery("SKU-1-0")));
    }

    [Fact]
    [Trait("Spec", "SRC-4")]
    public async Task Lookup_Sku_UsesTheUniqueIndexNotAScan()
    {
        using var host = await HostWithOneProductAsync();

        var plan = await host.QueryAsync(async db =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            // With one row the planner would scan anyway; forbid it to prove the index can answer the lookup.
            await db.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off");
            var lines = await db.Database.SqlQueryRaw<string>(
                "EXPLAIN SELECT \"ID\" FROM \"STFR\".\"PRDT_VRNT\" WHERE \"SKU_NORM\" = 'sku-1-0'").ToListAsync();
            return string.Join('\n', lines);
        });

        Assert.Contains("IX_PRDT_VRNT_SKU_NORM", plan);
    }
}
