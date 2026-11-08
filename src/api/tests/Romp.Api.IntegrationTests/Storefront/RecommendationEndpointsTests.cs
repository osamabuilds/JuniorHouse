using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Romp.Api.IntegrationTests.Production;
using Romp.Api.Seeding;
using Romp.Modules.Storefront.Application.Abstractions;
using Romp.Modules.Storefront.Application.ProductRecommendations;
using Romp.Modules.Storefront.Application.PublishedCatalog;

namespace Romp.Api.IntegrationTests.Storefront;

/// <summary>
/// Product recommendations task E1 and E2 through the real HTTP API (REC-1, REC-9 data, REC-12): anonymous, problem details, nothing leaked about
/// unknown products, the event body capped. Real PostgreSQL; the demo shop supplies the published products.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class RecommendationEndpointsTests(ProductionApiFixture api)
{
    private const string Url = "/api/storefront/products/recommendations";

    private async Task<string> SeededProductPathAsync()
    {
        await using (var scope = api.Services.CreateAsyncScope())
        {
            await DemoStorefrontSeeder.SeedAsync(scope.ServiceProvider, NullLogger.Instance, CancellationToken.None, 3);
        }

        await api.DispatchAsync();
        for (var i = 0; i < 2000; i++)
        {
            await using var scope = api.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessDerivativeCommand()) == DerivativeOutcome.NothingDue)
            {
                break;
            }
        }

        ((Romp.Modules.Storefront.Infrastructure.CatalogLookupCache)api.Services.GetRequiredService<ICatalogLookupCache>()).Invalidate();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunRecommendationPassCommand());
        }

        var listing = await api.Client.GetAsync("/api/storefront/listings?scope=collection&collection=winter&category=jackets");
        var page = JsonDocument.Parse(await listing.Content.ReadAsStringAsync()).RootElement;
        return page.GetProperty("tiles").EnumerateArray().First().GetProperty("path").GetString()!;
    }

    private async Task<(HttpStatusCode Status, string Body, string? ContentType)> GetAsync(string url)
    {
        var response = await api.Client.GetAsync(url);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(), response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [Trait("Spec", "REC-1")]
    [Trait("Spec", "REC-9")]
    public async Task APublishedProduct_AnswersBothRows_WithTheCardShapeAndNoCaching()
    {
        var path = await SeededProductPathAsync();

        var response = await api.Client.GetAsync($"{Url}?path={Uri.EscapeDataString(path)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var similar = body.GetProperty("similar").EnumerateArray().ToList();
        Assert.Equal(JsonValueKind.Array, body.GetProperty("trending").ValueKind);
        Assert.InRange(similar.Count, 1, 4);
        Assert.All(similar, card =>
        {
            Assert.NotEqual(path, card.GetProperty("path").GetString());
            Assert.True(card.TryGetProperty("sizes", out _) && card.TryGetProperty("name", out _));
        });
    }

    [Theory]
    [Trait("Spec", "REC-1")]
    [InlineData("")]
    [InlineData("?path=")]
    [InlineData("?path=%2Fa%3Fb")]
    [InlineData("?path=%2Fa%2F..%2Fb")]
    public async Task AMissingOrMalformedPath_Is400_WithProblemDetails(string query)
    {
        var (status, body, contentType) = await GetAsync($"{Url}{query}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("application/problem+json", contentType);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "REC-1")]
    public async Task AnUnknownPath_Is404_AndIsNotEchoed()
    {
        var (status, body, contentType) = await GetAsync($"{Url}?path=%2Fgirls%2Fdresses%2Fsecret-unpublished-thing");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("application/problem+json", contentType);
        Assert.DoesNotContain("secret-unpublished-thing", body, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "REC-12")]
    public async Task AnEvent_IsCounted_AndNeedsNoSignIn()
    {
        var path = await SeededProductPathAsync();
        var page = JsonDocument.Parse(await (await api.Client.GetAsync($"{Url}?path={Uri.EscapeDataString(path)}")).Content.ReadAsStringAsync()).RootElement;
        var productId = page.GetProperty("similar").EnumerateArray().First().GetProperty("productId").GetInt64();

        var response = await api.Client.PostAsJsonAsync($"{Url}/events", new { row = "SIMILAR", productId, action = "click" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [Trait("Spec", "REC-12")]
    [InlineData("{\"row\":\"NOPE\",\"productId\":1,\"action\":\"click\"}")]
    [InlineData("{\"row\":\"SIMILAR\",\"productId\":1,\"action\":\"hover\"}")]
    [InlineData("{\"row\":\"SIMILAR\",\"productId\":0,\"action\":\"click\"}")]
    [InlineData("{}")]
    public async Task ABadEvent_Is400_WithProblemDetails(string json)
    {
        var response = await api.Client.PostAsync($"{Url}/events", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    [Trait("Spec", "REC-12")]
    public async Task AnUnknownProduct_Is404_AndABodyOverOneKilobyte_IsRefused()
    {
        var unknown = await api.Client.PostAsJsonAsync($"{Url}/events", new { row = "SIMILAR", productId = 987_654_321L, action = "click" });
        var big = await api.Client.PostAsync($"{Url}/events",
            new StringContent("{\"row\":\"SIMILAR\",\"productId\":1,\"action\":\"click\",\"pad\":\"" + new string('x', 2000) + "\"}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, big.StatusCode);
    }
}
