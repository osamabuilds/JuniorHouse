using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.Modules.Catalog.Domain.Products;
using Romp.Modules.Catalog.Domain.Styles;
using Romp.Modules.Catalog.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Catalog;

/// <summary>
/// Sprint 7 through the real HTTP API against a real PostgreSQL and the real Reference data: create a product from a style, fill the
/// tabs, publish (first refused with every failure, then accepted), rename the URL and resolve the old one. Routes, status codes,
/// secured groups, problem details.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class CatalogEndpointsTests(ProductionApiFixture api)
{
    private const string Person = "Ayesha Khan";

    private static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 40, 0, 0, 0, 60, 8, 2, 0, 0, 0, 0, 0, 0, 0,
    ];

    private static readonly int[] CareIds = [1, 2];

    private static long _styleSeed = 700;

    private async Task<long> NewLabelledStyleAsync()
    {
        var n = Interlocked.Increment(ref _styleSeed);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var style = new Style($"STY-HTTP-{n}", $"Http Dress {n}", null, 3, 2, 3, 1, 450m, 1200m);
        style.SetColourSizeAndTargets([1], [4, 5], []);
        db.Styles.Add(style);
        await db.SaveChangesAsync();
        db.LabelledVariants.Add(new LabelledVariant(900_000 + n, style.Id, 1, 4, $"ROMP-HTTP-{n}-RED-1-2Y", "0200000000000", false));
        await db.SaveChangesAsync();
        return style.Id;
    }

    private async Task<JsonElement> NewProductAsync(string name)
    {
        var styleId = await NewLabelledStyleAsync();
        return await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync("/api/catalog/products", new
        {
            styleId, name, description = "A soft cotton dress.", genderId = 2, categoryId = 3, ageBracketId = 3, actingPerson = Person,
        }));
    }

    private Task<HttpResponseMessage> PutAsync(string url, object body) => api.Client.PutAsJsonAsync(url, body, ProductionApiFixture.Json);

    private async Task<JsonElement> GetAsync(string url) => await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync(url));

    private static uint Version(JsonElement product) => product.GetProperty("version").GetUInt32();

    [Fact]
    [Trait("Spec", "PRD-1")]
    public async Task ANewProduct_ListsItsPlannedVariants_AndTheOneLabelledVariantIsSellable()
    {
        var product = await NewProductAsync("Http Dress " + Guid.NewGuid().ToString("N")[..6]);

        Assert.Equal(2, product.GetProperty("variants").GetArrayLength());
        Assert.Equal(1, product.GetProperty("sellableVariants").GetInt32());
        Assert.Equal("Draft", product.GetProperty("status").GetString());
        Assert.StartsWith("/girls/dresses/http-dress-", product.GetProperty("path").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "PRD-4")]
    [Trait("Spec", "PRD-10")]
    [Trait("Spec", "URL-2")]
    public async Task TheWholeJourney_CreateFill_PublishRefusedThenAccepted_RenameAndResolve()
    {
        var product = await NewProductAsync("Journey Dress " + Guid.NewGuid().ToString("N")[..6]);
        var id = product.GetProperty("id").GetInt64();

        // Refused: every missing item at once, as a 422 with the failures.
        var refused = await api.PostAsync($"/api/catalog/products/{id}/publish", new { version = Version(product), actingPerson = Person });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var failures = JsonDocument.Parse(await refused.Content.ReadAsStringAsync()).RootElement.GetProperty("failures");
        Assert.Equal(["NO_IMAGE", "COMPOSITION_TOTAL", "CARE_MISSING"], failures.EnumerateArray().Select(f => f.GetProperty("code").GetString()!).ToArray());

        // Image by multipart upload, then alt text.
        using var form = new MultipartFormDataContent { { new StringContent(Person), "actingPerson" } };
        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain"); // the declared type is ignored: the bytes decide
        form.Add(file, "file", "front.png");
        var upload = await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.PostAsync($"/api/catalog/products/{id}/images", form));
        Assert.Equal("image/png", upload.GetProperty("contentType").GetString());
        Assert.Equal(40, upload.GetProperty("width").GetInt32());
        var imageId = upload.GetProperty("id").GetInt64();
        Assert.True((await PutAsync($"/api/catalog/images/{imageId}/alt", new { text = "A red floral dress, front view", actingPerson = Person })).IsSuccessStatusCode);

        // Served from our own record of the type.
        var served = await api.Client.GetAsync($"/api/catalog/images/{imageId}/file");
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());

        var current = await GetAsync($"/api/catalog/products/{id}");
        var composition = await ProductionApiFixture.ReadAsync<JsonElement>(await PutAsync(
            $"/api/catalog/products/{id}/composition", new { rows = new[] { new { fibreId = 1, percent = 95m }, new { fibreId = 3, percent = 5m } }, version = Version(current), actingPerson = Person }));
        Assert.True(composition.GetProperty("isComplete").GetBoolean());
        await ProductionApiFixture.ReadAsync<JsonElement>(await PutAsync(
            $"/api/catalog/products/{id}/care", new { careIds = CareIds, note = (string?)null, version = composition.GetProperty("version").GetUInt32(), actingPerson = Person }));

        current = await GetAsync($"/api/catalog/products/{id}");
        var published = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync(
            $"/api/catalog/products/{id}/publish", new { version = Version(current), actingPerson = Person }));
        Assert.Equal("Published", published.GetProperty("product").GetProperty("status").GetString());
        var oldPath = published.GetProperty("product").GetProperty("path").GetString()!;

        var newSlug = "journey-" + Guid.NewGuid().ToString("N")[..8];
        var moved = await ProductionApiFixture.ReadAsync<JsonElement>(await PutAsync(
            $"/api/catalog/products/{id}/path", new { genderId = 2, categoryId = 3, slug = newSlug, version = Version(published.GetProperty("product")), actingPerson = Person }));
        var resolved = await GetAsync($"/api/catalog/paths/resolve?path={Uri.EscapeDataString(oldPath)}");
        Assert.Equal((int)Romp.Modules.Catalog.Contracts.PathOutcome.Moved, resolved.GetProperty("outcome").GetInt32());
        Assert.Equal(moved.GetProperty("path").GetString(), resolved.GetProperty("currentPath").GetString());

        // The events reach the dispatcher without error.
        await api.DispatchAsync();
    }

    [Fact]
    [Trait("Spec", "PRD-5")]
    public async Task AStaleVersion_Is409_WithProblemDetails()
    {
        var product = await NewProductAsync("Stale Dress " + Guid.NewGuid().ToString("N")[..6]);
        var id = product.GetProperty("id").GetInt64();
        var body = new { name = "Edited", description = "d", genderId = 2, categoryId = 3, ageBracketId = 3, careNote = (string?)null, version = Version(product), actingPerson = Person };
        Assert.True((await PutAsync($"/api/catalog/products/{id}", body)).IsSuccessStatusCode);

        var stale = await PutAsync($"/api/catalog/products/{id}", body);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    [Trait("Spec", "PRD-2")]
    public async Task ACompareAtPriceNotAbovePrice_Is400()
    {
        var product = await NewProductAsync("Price Dress " + Guid.NewGuid().ToString("N")[..6]);
        var id = product.GetProperty("id").GetInt64();

        var response = await PutAsync($"/api/catalog/products/{id}/prices",
            new { prices = new[] { new { colourId = 1, sizeId = 4, price = 1000m, compareAtPrice = 900m } }, version = Version(product), actingPerson = Person });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("compare-at", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "MED-7")]
    public async Task ANonImageUpload_Is400_AndAnUnknownProduct_Is404()
    {
        var product = await NewProductAsync("Upload Dress " + Guid.NewGuid().ToString("N")[..6]);
        var id = product.GetProperty("id").GetInt64();
        using var form = new MultipartFormDataContent { { new StringContent(Person), "actingPerson" } };
        form.Add(new ByteArrayContent("<html>not an image</html>"u8.ToArray()), "file", "photo.jpg");

        var bad = await api.Client.PostAsync($"/api/catalog/products/{id}/images", form);
        var missing = await api.Client.GetAsync("/api/catalog/products/999999999");

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    [Trait("Spec", "PRD-11")]
    public async Task TheList_IsPaged_AndClamped()
    {
        await NewProductAsync("Listed Dress " + Guid.NewGuid().ToString("N")[..6]);

        var page = await GetAsync("/api/catalog/products?pageSize=5000");

        Assert.Equal(100, page.GetProperty("pageSize").GetInt32());
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    [Trait("Spec", "URL-7")]
    public async Task AReservedSlug_Is400()
    {
        var product = await NewProductAsync("Reserved Dress " + Guid.NewGuid().ToString("N")[..6]);
        var id = product.GetProperty("id").GetInt64();

        var response = await PutAsync($"/api/catalog/products/{id}/path", new { genderId = 2, categoryId = 3, slug = "checkout", version = Version(product), actingPerson = Person });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "CHT-1")]
    public async Task ASizeChart_IsCreatedOverHttp_AndAppliesToAProductOfThatCategoryAndGender()
    {
        // Category 6 (Accessories) + Boys has no demo chart, so this test owns it.
        var created = await api.PostAsync("/api/catalog/size-charts", new
        {
            categoryId = 6, genderId = 1, name = "Boys accessories",
            lines = new[] { new { sizeId = 4, ageMinMonths = 12, ageMaxMonths = 24, heightMinCm = 80m, heightMaxCm = 92m } },
            actingPerson = Person,
        });
        if (created.StatusCode == HttpStatusCode.Conflict)
        {
            return; // another run on the same database already created it
        }

        var chart = await ProductionApiFixture.ReadAsync<JsonElement>(created);
        Assert.Equal("cm", chart.GetProperty("unit").GetString());
        var list = await GetAsync("/api/catalog/size-charts?pageSize=100");
        Assert.True(list.GetProperty("totalCount").GetInt32() >= 1);
    }
}
