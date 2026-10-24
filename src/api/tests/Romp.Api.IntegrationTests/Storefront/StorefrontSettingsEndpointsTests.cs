using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Storefront;

/// <summary>
/// SCRUM-206 tasks 12 and 13 through the real HTTP API against a real PostgreSQL: routes, status codes (400, 409, 304, 301, 410),
/// problem details and the public cache headers. The development user holds every role in "Testing", so the role checks are proved in
/// <c>StorefrontAdminSecurityTests</c>; here the routes are exercised as an admin.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class StorefrontSettingsEndpointsTests(ProductionApiFixture api)
{
    private const string Admin = "/api/admin/storefront-settings";

    private static readonly JsonSerializerOptions Json = ProductionApiFixture.Json;

    private async Task<JsonElement> GetAsync(string url) => await ProductionApiFixture.ReadAsync<JsonElement>(await api.Client.GetAsync(url));

    private Task<HttpResponseMessage> PutAsync(string url, object body) => api.Client.PutAsJsonAsync(url, body, Json);

    private async Task<int> DraftVersionAsync() => (await GetAsync($"{Admin}/draft")).GetProperty("version").GetInt32();

    [Fact]
    [Trait("Spec", "ST-14")]
    public async Task ASaveQuotingAStaleDraftVersion_Is409_WithProblemDetails_AndChangesNothing()
    {
        var version = await DraftVersionAsync();
        var ages = (await GetAsync($"{Admin}/draft")).GetProperty("ages").EnumerateArray().Select(a => JsonSerializer.Deserialize<object>(a.GetRawText(), Json)).ToList();
        Assert.True((await PutAsync($"{Admin}/ages", new { expectedVersion = version, steps = ages })).IsSuccessStatusCode);

        var stale = await PutAsync($"{Admin}/ages", new { expectedVersion = version, steps = ages });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("changed the settings", await stale.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(version + 1, await DraftVersionAsync());
    }

    [Fact]
    [Trait("Spec", "ST-14")]
    public async Task APageSaveQuotingAStaleRowVersion_Is409()
    {
        var created = await ProductionApiFixture.ReadAsync<JsonElement>(await api.PostAsync($"{Admin}/pages",
            new { slug = "it-row-version", title = "Row version", metaDescription = "Test page.", blocks = Array.Empty<object>() }));
        var id = created.GetProperty("id").GetInt64();
        var rowVersion = created.GetProperty("rowVersion").GetUInt32();
        var body = new { rowVersion, slug = "it-row-version", title = "Row version", metaDescription = "Changed once.", blocks = Array.Empty<object>() };
        Assert.Equal(HttpStatusCode.OK, (await PutAsync($"{Admin}/pages/{id}", body)).StatusCode);

        var stale = await PutAsync($"{Admin}/pages/{id}", body);

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    [Trait("Spec", "ST-4")]
    public async Task AFourthNavigationLevel_Is400_WithTheFieldAndTheLevelsMessage()
    {
        object Item(string key, object[] children) => new
        {
            key, label = key, kind = "Link", target = new { kind = "Path", reference = "/" + key }, sort = 1, enabled = true, showAges = false, children,
        };
        var tree = Item("aa", [Item("bb", [Item("cc", [Item("dd", [])])])]);

        var response = await PutAsync($"{Admin}/navigation", new { expectedVersion = await DraftVersionAsync(), items = new[] { tree } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("navigation.aa.bb.cc.dd", text, StringComparison.Ordinal);
        Assert.Contains("Only 3 levels are allowed", text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "ST-9")]
    public async Task ABadPageBody_AndAnUnknownPage_AreRefusedCleanly()
    {
        var reserved = await api.PostAsync($"{Admin}/pages", new { slug = "api", title = "T", metaDescription = "D", blocks = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, reserved.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync($"{Admin}/pages/999999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client.GetAsync("/api/storefront/pages/does-not-exist")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "ST-8")]
    public async Task ThePagedListsClampTheirSize()
    {
        var pages = await GetAsync($"{Admin}/pages?page=-3&pageSize=5000");
        Assert.Equal(100, pages.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, pages.GetProperty("page").GetInt32());

        var snapshots = await GetAsync($"{Admin}/snapshots?pageSize=0");
        Assert.Equal(1, snapshots.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    [Trait("Spec", "ST-1")]
    [Trait("Spec", "ST-3")]
    [Trait("Spec", "ST-13")]
    public async Task PublishingMakesTheSettingsAndPagesPublic_WithACacheableETag()
    {
        // Every seeded page becomes publishable (the two legal placeholders get a description), then the whole draft is published.
        var list = await GetAsync($"{Admin}/pages?pageSize=100");
        foreach (var row in list.GetProperty("items").EnumerateArray())
        {
            var page = await GetAsync($"{Admin}/pages/{row.GetProperty("id").GetInt64()}");
            var rowVersion = page.GetProperty("rowVersion").GetUInt32();
            if (page.GetProperty("metaDescription").GetString() is { Length: 0 })
            {
                var updated = await ProductionApiFixture.ReadAsync<JsonElement>(await PutAsync($"{Admin}/pages/{page.GetProperty("id").GetInt64()}", new
                {
                    rowVersion, slug = page.GetProperty("slug").GetString(), title = page.GetProperty("title").GetString(),
                    metaDescription = "Information.", blocks = Array.Empty<object>(),
                }));
                rowVersion = updated.GetProperty("rowVersion").GetUInt32();
            }

            if (page.GetProperty("status").GetString() != "Published")
            {
                Assert.Equal(HttpStatusCode.OK, (await PutAsync($"{Admin}/pages/{page.GetProperty("id").GetInt64()}/status", new { rowVersion, status = "Published" })).StatusCode);
            }
        }

        var publish = await api.PostAsync($"{Admin}/publish", new { expectedVersion = await DraftVersionAsync(), note = "integration" });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var settings = await api.Client.GetAsync("/api/storefront/settings");
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        var etag = settings.Headers.ETag!.Tag;
        Assert.Equal("public, max-age=60", settings.Headers.CacheControl!.ToString());
        Assert.Equal("Romp", JsonDocument.Parse(await settings.Content.ReadAsStringAsync()).RootElement.GetProperty("brand").GetProperty("name").GetString());

        var conditional = new HttpRequestMessage(HttpMethod.Get, "/api/storefront/settings");
        conditional.Headers.TryAddWithoutValidation("If-None-Match", etag);
        Assert.Equal(HttpStatusCode.NotModified, (await api.Client.SendAsync(conditional)).StatusCode);

        var delivery = await api.Client.GetAsync("/api/storefront/pages/delivery");
        Assert.Equal(HttpStatusCode.OK, delivery.StatusCode);
        Assert.Equal("delivery", JsonDocument.Parse(await delivery.Content.ReadAsStringAsync()).RootElement.GetProperty("slug").GetString());
    }
}
