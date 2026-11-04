using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Infrastructure.Persistence;
using Romp.Modules.Storefront.Application.Abstractions;

namespace Romp.Api.IntegrationTests.Storefront;

/// <summary>
/// Published-catalog task 24: publish a product in Catalog through the real HTTP API and see it in the storefront's own read model, fed only by
/// the outbox event and Catalog's contract (PCT-1). Real PostgreSQL, real dispatcher, no fakes.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class PublishedCatalogEndToEndTests(ProductionApiFixture api)
{
    private const string Person = "Ayesha Khan";
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);

    private static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 40, 0, 0, 0, 60, 8, 2, 0, 0, 0, 0, 0, 0, 0,
    ];

    private static readonly int[] CareIds = [1, 2];

    private static long _styleSeed = 7_000;

    [Fact]
    [Trait("Spec", "PCT-1")]
    public async Task APublishedProduct_AppearsInTheReadModel_AndLeavesWhenUnpublished()
    {
        var (id, path, sku) = await CreatePublishedProductAsync();

        // Poll until the product shows up instead of timing one dispatch pass: under load (CI, every module's audit messages sharing the
        // dispatcher) a single pass can legitimately take longer, and a fixed wait would flake. The ceiling only stops a hung run.
        var clock = Stopwatch.StartNew();
        var row = await PollAsync(path, present: true);
        clock.Stop();

        Assert.NotNull(row);
        Assert.True(clock.Elapsed < Ceiling, $"The read model took {clock.Elapsed.TotalSeconds:F1} s to show the product.");
        Assert.Equal(id, row.Id);
        Assert.Equal(sku, row.Skus.Single());
        Assert.True(row.ContentVersion >= 1);

        var unpublished = await api.PostAsync($"/api/catalog/products/{id}/unpublish", new { version = await CurrentVersionAsync(id), actingPerson = Person });
        Assert.True(unpublished.IsSuccessStatusCode, await unpublished.Content.ReadAsStringAsync());

        Assert.Null(await PollAsync(path, present: false));
    }

    /// <summary>Runs the dispatcher and looks again every 100 ms until the product is (or is no longer) in the read model, or the ceiling passes.</summary>
    private async Task<Found?> PollAsync(string path, bool present)
    {
        var deadline = Stopwatch.StartNew();
        Found? row;
        do
        {
            await api.DispatchAsync();
            row = await FindAsync(path);
            if ((row is not null) == present)
            {
                return row;
            }

            await Task.Delay(100);
        }
        while (deadline.Elapsed < Ceiling);

        return row;
    }

    private async Task<(long Id, string Path, string Sku)> CreatePublishedProductAsync()
    {
        var n = Interlocked.Increment(ref _styleSeed);
        long styleId;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var style = new Style($"STY-E2E-{n}", $"E2e Dress {n}", null, 3, 2, 3, 1, 450m, 1200m);
            style.SetColourSizeAndTargets([1], [4, 5], []);
            db.Styles.Add(style);
            await db.SaveChangesAsync();
            db.LabelledVariants.Add(new LabelledVariant(950_000 + n, style.Id, 1, 4, $"ROMP-E2E-{n}-RED-1-2Y", "0200000000000", false));
            await db.SaveChangesAsync();
            styleId = style.Id;
        }

        var product = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync("/api/catalog/products", new
        {
            styleId, name = "E2e Dress " + Guid.NewGuid().ToString("N")[..6], description = "A soft cotton dress.", genderId = 2, categoryId = 3, ageBracketId = 3, actingPerson = Person,
        }));
        var id = product.GetProperty("id").GetInt64();

        using var form = new MultipartFormDataContent { { new StringContent(Person), "actingPerson" } };
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "front.png");
        var upload = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.PostAsync($"/api/catalog/products/{id}/images", form));
        Assert.True((await api.Client.PutAsJsonAsync($"/api/catalog/images/{upload.GetProperty("id").GetInt64()}/alt", new { text = "A red floral dress, front view", actingPerson = Person }, ProductionApiFixture.Json)).IsSuccessStatusCode);

        var composition = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.PutAsJsonAsync(
            $"/api/catalog/products/{id}/composition",
            new { rows = new[] { new { fibreId = 1, percent = 95m }, new { fibreId = 3, percent = 5m } }, version = await CurrentVersionAsync(id), actingPerson = Person },
            ProductionApiFixture.Json));
        await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.PutAsJsonAsync(
            $"/api/catalog/products/{id}/care",
            new { careIds = CareIds, note = (string?)null, version = composition.GetProperty("version").GetUInt32(), actingPerson = Person },
            ProductionApiFixture.Json));

        var published = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync(
            $"/api/catalog/products/{id}/publish", new { version = await CurrentVersionAsync(id), actingPerson = Person }));
        return (id, published.GetProperty("product").GetProperty("path").GetString()!, $"ROMP-E2E-{n}-RED-1-2Y");
    }

    private async Task<uint> CurrentVersionAsync(long id) =>
        (await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync($"/api/catalog/products/{id}"))).GetProperty("version").GetUInt32();

    private async Task<Found?> FindAsync(string path)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IStorefrontReadDbContext>();
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Path == path);
        if (product is null)
        {
            return null;
        }

        var skus = await db.Variants.AsNoTracking().Where(v => v.ProductId == product.Id && v.SkuCode != null).Select(v => v.SkuCode!).ToListAsync();
        return new Found(product.Id, product.ContentVersion, skus);
    }

    private sealed record Found(long Id, long ContentVersion, IReadOnlyList<string> Skus);
}
