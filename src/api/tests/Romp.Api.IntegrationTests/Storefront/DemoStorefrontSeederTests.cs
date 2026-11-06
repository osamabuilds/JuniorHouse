using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Romp.Api.IntegrationTests.Production;
using Romp.Api.Seeding;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.PublishedCatalog;

namespace Romp.Api.IntegrationTests.Storefront;

/// <summary>
/// Sold items in listings task 20 (SLD-14): the Development demo shop has winter and summer ranges with in-stock, part sold and sold-out
/// pieces, made through the real commands, flowing to the read model by events, and the seeder can run twice. Real PostgreSQL.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class DemoStorefrontSeederTests(ProductionApiFixture api)
{
    private const int PiecesPerCategory = 3; // one full, one with a size gone, one sold out

    [Fact]
    [Trait("Spec", "SLD-14")]
    public async Task TheDemoShop_HasWinterAndSummerRanges_WithSoldOutPiecesLastInTheListing_AndSeedsOnce()
    {
        for (var pass = 0; pass < 2; pass++)
        {
            await using var scope = api.Services.CreateAsyncScope();
            await DemoStorefrontSeeder.SeedAsync(scope.ServiceProvider, NullLogger.Instance, CancellationToken.None, PiecesPerCategory);
        }

        await api.DispatchAsync();
        await MakeDerivativesAsync();

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var expected = DemoStorefrontSeeder.Seasons.Length * DemoStorefrontSeeder.Categories.Length * PiecesPerCategory;
            Assert.Equal(expected, await catalog.Styles.CountAsync(s => s.Code.StartsWith(DemoStorefrontSeeder.StyleCodePrefix))); // twice, not doubled

            var read = scope.ServiceProvider.GetRequiredService<IStorefrontReadDbContext>();
            var collections = await read.Collections.AsNoTracking().Where(c => c.CollectionCode == "WINTER" || c.CollectionCode == "SUMMER").ToListAsync();
            Assert.Equal(expected, collections.Count);
            Assert.Equal(expected / 2, collections.Count(c => c.CollectionCode == "WINTER"));

            var ids = collections.Select(c => c.ProductId).ToList();
            var products = await read.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync();
            Assert.Contains(products, p => !p.IsAvailable); // sold out
            Assert.Contains(products, p => p.IsAvailable);
            Assert.All(products.Where(p => !p.IsAvailable), p => Assert.True(p.IsListable && p.ImageReady)); // sold out stays on the shelf
        }

        // The winter jackets page: all three pieces are there, the sold-out one comes last and says so.
        var response = await api.Client.GetAsync("/api/storefront/listings?scope=collection&collection=winter&category=jackets");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var tiles = page.GetProperty("tiles").EnumerateArray().ToList();
        Assert.Equal(PiecesPerCategory, tiles.Count);
        var soldOut = tiles.Select(t => t.TryGetProperty("soldOut", out var s) && s.GetBoolean()).ToList();
        Assert.True(soldOut[^1]);
        Assert.False(soldOut[0]);
        Assert.Equal(PiecesPerCategory, page.GetProperty("total").GetInt32());
    }

    /// <summary>Makes the resized pictures now instead of waiting for the background worker (a piece shows in listings once its picture is ready).</summary>
    private async Task MakeDerivativesAsync()
    {
        for (var i = 0; i < 2000; i++)
        {
            await using var scope = api.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessDerivativeCommand()) == DerivativeOutcome.NothingDue)
            {
                return;
            }
        }
    }
}
