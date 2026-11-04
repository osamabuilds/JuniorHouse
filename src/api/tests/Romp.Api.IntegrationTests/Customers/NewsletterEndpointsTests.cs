using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Api.IntegrationTests.Production;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Customers.Application;
using Romp.Modules.Customers.Domain.Newsletter;
using Romp.Modules.Customers.Infrastructure.Persistence;

namespace Romp.Api.IntegrationTests.Customers;

/// <summary>
/// customers/newsletter NL7 through the real host (NLS-2, NLS-3, NLS-6): the same answer for every address, links that name one record,
/// the account toggle on the caller's own address, and no caching. The host signs links with the development key. The stand-in is customer
/// one; <c>X-Dev-Customer: other</c> is the seeded second customer and <c>none</c> is a guest.
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class NewsletterEndpointsTests(ProductionApiFixture api)
{
    private static string NewEmail() => $"http.nl.{Guid.NewGuid():N}@example.test";

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, string? customer = null, string? origin = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (customer is not null)
        {
            request.Headers.Add("X-Dev-Customer", customer);
        }

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: ProductionApiFixture.Json);
        }

        return await api.Client.SendAsync(request);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(), ProductionApiFixture.Json);

    private async Task<(long Id, NewsletterStatus Status)?> StoredAsync(string email)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustDbContext>();
        var row = await db.Newsletter.AsNoTracking().Where(s => s.Email == email).Select(s => new { s.Id, s.Status }).SingleOrDefaultAsync();
        return row is null ? null : (row.Id, row.Status);
    }

    private static string TokenFor(long id) => Uri.EscapeDataString(UnsubscribeToken.Create(id, CustomersOptions.DevelopmentLinkSigningKey));

    [Fact]
    [Trait("Spec", "NLS-6")]
    public async Task Subscribe_AnswersTheSame_ForANewAndARepeatedAddress_AndIsNoStore()
    {
        var email = NewEmail();

        var first = await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email });
        var again = await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email = email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await again.Content.ReadAsStringAsync());
        Assert.Equal("no-store", first.Headers.CacheControl?.ToString());
        Assert.Equal(NewsletterStatus.Subscribed, (await StoredAsync(email))!.Value.Status);
    }

    [Fact]
    [Trait("Spec", "NLS-6")]
    public async Task Subscribe_AnswersTheSame_ForAnAddressThatHadUnsubscribed()
    {
        var email = NewEmail();
        var fresh = await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email });
        var id = (await StoredAsync(email))!.Value.Id;
        await SendAsync(HttpMethod.Get, $"/api/newsletter/unsubscribe?token={TokenFor(id)}");

        var back = await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email });

        Assert.Equal(await fresh.Content.ReadAsStringAsync(), await back.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, back.StatusCode);
    }

    [Theory]
    [Trait("Spec", "NLS-1")]
    [InlineData("not an email")]
    [InlineData("")]
    public async Task Subscribe_WithAnUnreadableAddress_Is400(string email)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Spec", "NLS-6")]
    public async Task Subscribe_FromAnotherSite_IsRefused()
    {
        var email = NewEmail();

        var response = await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email }, origin: "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(await StoredAsync(email));
    }

    [Fact]
    [Trait("Spec", "NLS-6")]
    public void ThePublicSubscribeRoute_IsOnTheStrictRateLimit()
    {
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/newsletter/", StringComparison.Ordinal) == true)
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, e => Assert.Contains(e.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>(), a => a.PolicyName == RateLimitPolicies.Strict));
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public async Task TheUnsubscribeLink_WorksWithoutSigningIn_ByGetAndByOneClickPost()
    {
        var byGet = NewEmail();
        var byPost = NewEmail();
        await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email = byGet });
        await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email = byPost });

        var get = await SendAsync(HttpMethod.Get, $"/api/newsletter/unsubscribe?token={TokenFor((await StoredAsync(byGet))!.Value.Id)}", customer: "none");
        var post = await SendAsync(HttpMethod.Post, $"/api/newsletter/unsubscribe?token={TokenFor((await StoredAsync(byPost))!.Value.Id)}", customer: "none");

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        Assert.Equal("no-store", get.Headers.CacheControl?.ToString());
        Assert.Equal(NewsletterStatus.Unsubscribed, (await StoredAsync(byGet))!.Value.Status);
        Assert.Equal(NewsletterStatus.Unsubscribed, (await StoredAsync(byPost))!.Value.Status);
    }

    [Fact]
    [Trait("Spec", "NLS-3")]
    public async Task TheUnsubscribeLink_RepeatsHarmlessly_AndAnOtherRecordsLinkLeavesThisOneAlone()
    {
        var mine = NewEmail();
        var theirs = NewEmail();
        await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email = mine });
        await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email = theirs });
        var theirLink = $"/api/newsletter/unsubscribe?token={TokenFor((await StoredAsync(theirs))!.Value.Id)}";

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, theirLink)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, theirLink)).StatusCode);

        Assert.Equal(NewsletterStatus.Subscribed, (await StoredAsync(mine))!.Value.Status);
        Assert.Equal(NewsletterStatus.Unsubscribed, (await StoredAsync(theirs))!.Value.Status);
    }

    [Theory]
    [Trait("Spec", "NLS-3")]
    [InlineData("")]
    [InlineData("?token=")]
    [InlineData("?token=abc")]
    [InlineData("?token=MQ.AAAA")]
    public async Task AForgedOrBrokenLink_Is400_AndChangesNothing(string query)
    {
        var email = NewEmail();
        await SendAsync(HttpMethod.Post, "/api/newsletter/subscribe", new { email });

        var response = await SendAsync(HttpMethod.Get, "/api/newsletter/unsubscribe" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(NewsletterStatus.Subscribed, (await StoredAsync(email))!.Value.Status);
    }

    [Fact]
    [Trait("Spec", "NLS-2")]
    public async Task TheAccountToggle_ReadsAndSetsTheCallersOwnPreference_AndIsNoStore()
    {
        // The stand-in customers have fixed addresses; start both from off so the test can run again on the same database.
        await SendAsync(HttpMethod.Put, "/api/customers/me/newsletter", new { subscribed = false });
        await SendAsync(HttpMethod.Put, "/api/customers/me/newsletter", new { subscribed = false }, customer: "other");

        var on = await SendAsync(HttpMethod.Put, "/api/customers/me/newsletter", new { subscribed = true });

        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.Equal("no-store", on.Headers.CacheControl?.ToString());
        Assert.True((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/customers/me/newsletter"))).GetProperty("subscribed").GetBoolean());
        Assert.False((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/customers/me/newsletter", customer: "other"))).GetProperty("subscribed").GetBoolean());

        await SendAsync(HttpMethod.Put, "/api/customers/me/newsletter", new { subscribed = false });
        Assert.False((await JsonAsync(await SendAsync(HttpMethod.Get, "/api/customers/me/newsletter"))).GetProperty("subscribed").GetBoolean());
    }

    [Fact]
    [Trait("Spec", "NLS-2")]
    public async Task TheAccountToggle_NeedsASignedInCustomer()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/customers/me/newsletter", customer: "none")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Put, "/api/customers/me/newsletter", new { subscribed = true }, customer: "none")).StatusCode);
    }
}
