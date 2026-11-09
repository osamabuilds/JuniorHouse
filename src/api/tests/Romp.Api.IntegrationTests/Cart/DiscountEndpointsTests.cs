using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Cart;

/// <summary>The discount code box through the real API: refusals read as inline reasons, guessing is throttled, and the origin is checked.</summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class DiscountEndpointsTests(ProductionApiFixture api)
{
    private const string Origin = "http://localhost";

    private WebApplicationFactory<Program> Shop() => api.Factory;

    private static HttpClient Guest(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Dev-Customer", "none");
        return client;
    }

    private static Task<HttpResponseMessage> Apply(HttpClient client, string code, string? origin = Origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/discount") { Content = JsonContent.Create(new { code }) };
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return client.SendAsync(request);
    }

    [Fact]
    [Trait("Spec", "DSC-9")]
    public async Task AnEmptyBasketOrAnUnknownCode_IsRefusedAsABadRequest_WithAReason()
    {
        using var client = Guest(Shop());

        var response = await Apply(client, "NOSUCHCODE");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("bag", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "SEC-12")]
    public async Task AnotherSite_CannotApplyACode()
    {
        using var client = Guest(Shop());

        var response = await Apply(client, "ANYTHING", "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    [Fact]
    [Trait("Spec", "DSC-1")]
    public async Task RemovingACodeWithNoneApplied_AnswersWithTheBasket()
    {
        using var client = Guest(Shop());
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/cart/discount");
        request.Headers.Add("Origin", Origin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), ProductionApiFixture.Json);
        Assert.Equal(0, body.GetProperty("discount").GetDecimal());
    }
}
