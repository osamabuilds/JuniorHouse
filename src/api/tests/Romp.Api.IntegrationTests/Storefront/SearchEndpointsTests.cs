using System.Net;
using System.Text.Json;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Storefront;

/// <summary>Search task 8 through the real HTTP API: anonymous, hostile text is plain text, no SQL or stack trace ever leaks (SRC-2, 3, 4).</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class SearchEndpointsTests(ProductionApiFixture api)
{
    private const string Search = "/api/storefront/search";

    private async Task<(HttpStatusCode Status, string Body, string CacheControl)> GetAsync(string url)
    {
        var response = await api.Client.GetAsync(url);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.CacheControl?.ToString() ?? string.Empty);
    }

    private static void AssertNoLeak(string body)
    {
        foreach (var marker in new[] { "   at ", "Npgsql", "SELECT ", "\"STFR\"", "Exception", "tsquery" })
        {
            Assert.DoesNotContain(marker, body, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Spec", "SRC-2")]
    public async Task Gibberish_IsAnEmptyOk_WithNoTilesAndNoCaching()
    {
        var (status, body, cache) = await GetAsync($"{Search}?q=qzxjvk");

        Assert.Equal(HttpStatusCode.OK, status);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal(0, json.GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Array, json.GetProperty("tiles").ValueKind);
        Assert.Equal("qzxjvk", json.GetProperty("query").GetString());
        Assert.Contains("no-store", cache, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Spec", "SRC-3")]
    [InlineData("%27%20OR%201%3D1%20--")]
    [InlineData("%3Cscript%3Ealert(1)%3C%2Fscript%3E")]
    [InlineData("%26%20%7C%20!%20(%20)%20%3A*")]
    [InlineData("%00%E2%80%AE%E2%80%8B")]
    [InlineData("%F0%9F%98%80%F0%9F%98%80")]
    [InlineData("%ED%A0%80")]
    [InlineData("")]
    public async Task HostileText_IsHandledAsPlainText_WithNoErrorTextAndNoSql(string encoded)
    {
        var (status, body, _) = await GetAsync($"{Search}?q={encoded}");

        Assert.Equal(HttpStatusCode.OK, status);
        AssertNoLeak(body);
    }

    [Fact]
    [Trait("Spec", "SRC-3")]
    public async Task VeryLongText_IsCutAndAnswered()
    {
        var (status, body, _) = await GetAsync($"{Search}?q={new string('a', 6000)}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(JsonDocument.Parse(body).RootElement.GetProperty("query").GetString()!.Length <= 100);
    }

    [Theory]
    [Trait("Spec", "SRC-3")]
    [InlineData("page=abc")]
    [InlineData("page=1e309")]
    [InlineData("pageSize=-5")]
    [InlineData("price=abc-10")]
    public async Task MalformedNumbers_Are400_WithNoStackTrace(string query)
    {
        var (status, body, _) = await GetAsync($"{Search}?q=frock&{query}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        AssertNoLeak(body);
    }

    [Fact]
    [Trait("Spec", "SRC-1")]
    public async Task Suggest_IsAnonymous_AndAnswersAnEmptyListForNothingTyped()
    {
        var (status, body, _) = await GetAsync($"{Search}/suggest?q=");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(body).RootElement.GetProperty("items").ValueKind);
    }

    [Theory]
    [Trait("Spec", "SRC-3")]
    [InlineData("%27%20OR%201%3D1%20--")]
    [InlineData("%26%20%7C%20!%20(%20)%20%3A*")]
    [InlineData("%3Cscript%3E")]
    public async Task Suggest_HostileText_IsNotAnError(string encoded)
    {
        var (status, body, _) = await GetAsync($"{Search}/suggest?q={encoded}");

        Assert.Equal(HttpStatusCode.OK, status);
        AssertNoLeak(body);
    }

    [Theory]
    [Trait("Spec", "SRC-4")]
    [InlineData("")]
    [InlineData("?code=")]
    [InlineData("?code=ROMP-NOPE-RED-2Y")]
    [InlineData("?code=%27%20OR%201%3D1%20--")]
    public async Task Sku_UnknownOrMissing_Is404_WithNoStackTrace(string query)
    {
        var (status, body, _) = await GetAsync($"{Search}/sku{query}");

        Assert.Equal(HttpStatusCode.NotFound, status);
        AssertNoLeak(body);
    }
}
