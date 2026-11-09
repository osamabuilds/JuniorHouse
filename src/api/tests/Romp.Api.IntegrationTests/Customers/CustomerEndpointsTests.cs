using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Web;

namespace Romp.Api.IntegrationTests.Customers;

/// <summary>
/// The customer's own account through the real host (customers/accounts, ACT-14 and ACT-18; ADR 0021): the development-only stand-in is
/// the signed-in customer, can be switched off, and no route takes a customer id.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class CustomerEndpointsTests(ProductionApiFixture api)
{
    private const string Me = "/api/customers/me";

    [Fact]
    [Trait("Spec", "ACT-14")]
    public async Task Me_ReturnsTheStandIn()
    {
        var response = await api.Client.GetAsync(Me);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), ProductionApiFixture.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("dev.customer@example.test", body.GetProperty("email").GetString());
        Assert.Equal("Dev", body.GetProperty("firstName").GetString());
        Assert.True(body.GetProperty("id").GetInt64() > 0);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "ACT-14")]
    public async Task Me_IsUnauthorized_WhenTheRequestSaysNobodyIsSignedIn()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Me);
        request.Headers.Add("X-Dev-Customer", "none");

        var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "ACT-14")]
    public async Task TheDevSwitch_TurnsTheStandInOffAndOnForABrowser()
    {
        using var browser = api.Factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.GetAsync("/dev/customer?state=off")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.GetAsync("/dev/customer?state=on")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync(Me)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.GetAsync("/dev/customer?state=maybe")).StatusCode);
    }

    [Fact]
    [Trait("Spec", "ACT-18")]
    public void EveryCustomerRoute_IsInTheCustomerPolicy_AndTakesNoCustomerId()
    {
        var routes = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/customers", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        Assert.NotEmpty(routes);

        foreach (var route in routes)
        {
            var policies = route.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList();
            // The sign-in routes (register, sign-in, reset ...) are public by nature: they have no session yet. Every other route is customer-only.
            if (route.RoutePattern.RawText!.StartsWith("/api/customers/auth", StringComparison.OrdinalIgnoreCase) && policies.Contains(Policies.Public))
            {
                continue;
            }

            Assert.Contains(Policies.Customer, policies);

            var text = route.RoutePattern.RawText!;
            Assert.DoesNotContain("customerId", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("custId", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("userId", text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
