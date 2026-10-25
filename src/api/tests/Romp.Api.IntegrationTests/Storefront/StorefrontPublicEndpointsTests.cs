using System.Net;
using System.Text.Json;
using Romp.Api.IntegrationTests.Production;

namespace Romp.Api.IntegrationTests.Storefront;

/// <summary>
/// Listings task 6 and navigation task 5 through the real HTTP API: anonymous, clean errors, nothing echoed, no stack trace (LST-11, 14).
/// </summary>
[Collection(ProductionApiCollectionDefinition.Name)]
public sealed class StorefrontPublicEndpointsTests(ProductionApiFixture api)
{
    private const string Listings = "/api/storefront/listings";

    private async Task<(HttpStatusCode Status, string Body)> GetAsync(string url)
    {
        var response = await api.Client.GetAsync(url);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Spec", "NAV-1")]
    public async Task TheMenu_IsPublic_AndAnswersTheDocumentedShape()
    {
        var (status, body) = await GetAsync("/api/storefront/menu");

        Assert.Equal(HttpStatusCode.OK, status);
        var menu = JsonDocument.Parse(body).RootElement;
        Assert.Equal(JsonValueKind.Array, menu.GetProperty("audiences").ValueKind);
        Assert.Equal(JsonValueKind.Array, menu.GetProperty("collections").ValueKind);
        Assert.True(menu.TryGetProperty("hasNewIn", out _) && menu.TryGetProperty("hasSale", out _));
    }

    [Fact]
    [Trait("Spec", "NAV-10")]
    public async Task TheHome_IsPublic_AndAnswersTilesAndAudiences()
    {
        var (status, body) = await GetAsync("/api/storefront/home");

        Assert.Equal(HttpStatusCode.OK, status);
        var home = JsonDocument.Parse(body).RootElement;
        Assert.Equal(JsonValueKind.Array, home.GetProperty("newArrivals").ValueKind);
        Assert.Equal(JsonValueKind.Array, home.GetProperty("audiences").ValueKind);
    }

    [Theory]
    [Trait("Spec", "LST-11")]
    [InlineData("scope=audience")] // gender missing
    [InlineData("scope=bogus")]
    [InlineData("")]
    [InlineData("scope=new-in&price=-5-10")]
    [InlineData("scope=new-in&price=1e309-5")]
    [InlineData("scope=new-in&price=abc")]
    [InlineData("scope=new-in&page=abc")]
    [InlineData("scope=new-in&page=0")]
    [InlineData("scope=new-in&page=-1")]
    [InlineData("scope=new-in&pageSize=-4")]
    public async Task MalformedInput_Is400_WithProblemDetails_AndNoStackTrace(string query)
    {
        var (status, body) = await GetAsync($"{Listings}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
        Assert.Equal("application/problem+json", (await api.Client.GetAsync($"{Listings}?{query}")).Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [Trait("Spec", "LST-11")]
    [InlineData("scope=audience&gender=martians")]
    [InlineData("scope=category&gender=girls&category=nope")]
    [InlineData("scope=collection&collection=nope")]
    public async Task AnUnknownOrHostileCode_Is404_AndIsNeverEchoed(string query)
    {
        var (status, body) = await GetAsync($"{Listings}?{query}");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.DoesNotContain("script", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("etc", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Spec", "LST-11")]
    [InlineData("scope=audience&gender=%3Cscript%3Ealert(1)%3C%2Fscript%3E")]
    [InlineData("scope=audience&gender=..%2F..%2Fetc")]
    public async Task AHostileCode_IsRefused_AndIsNeverEchoed(string query)
    {
        var (status, body) = await GetAsync($"{Listings}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.DoesNotContain("script", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("etc", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "LST-14")]
    public async Task AHugePageNumber_Is404_WithoutScanning()
    {
        var (status, _) = await GetAsync($"{Listings}?scope=new-in&page=999999999");

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    [Trait("Spec", "LST-11")]
    public async Task AValidListing_ReturnsNoStockCounts_AndUnknownKeysAreIgnored()
    {
        var (status, body) = await GetAsync($"{Listings}?scope=new-in&utm_source=x&size=5-6y&sort=price-asc");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.DoesNotContain("quantity", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(body).RootElement.GetProperty("tiles").ValueKind);
    }

    [Fact]
    [Trait("Spec", "LST-1")]
    public async Task TheOptionsAndEmptyEndpoints_AreAnonymous_AndShareTheScopeRules()
    {
        Assert.Equal(HttpStatusCode.OK, (await GetAsync($"{Listings}/options?scope=new-in")).Status);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync($"{Listings}/empty?scope=new-in")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"{Listings}/options?scope=audience&gender=martians")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await GetAsync($"{Listings}/empty?scope=bogus")).Status);
    }

    [Theory]
    [Trait("Spec", "PCT-7")]
    [InlineData("not-a-name")]
    [InlineData("../appsettings.json")]
    [InlineData("0123456789abcdef0123456789abcdef-320.png")]
    [InlineData("0123456789abcdef0123456789abcdef-320.webp")] // well-formed, but no such file
    public async Task TheMediaRoute_ServesOnlyGeneratedNames_AndAnythingElseIs404(string name)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/media/" + Uri.EscapeDataString(name))).Status);
    }

    private const string Products = "/api/storefront/products/by-path";

    [Theory]
    [Trait("Spec", "PDP-13")]
    [InlineData("")]
    [InlineData("?path=")]
    [InlineData("?path=%20")]
    public async Task AMissingPath_Is400(string query) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await GetAsync(Products + query)).Status);

    [Fact]
    [Trait("Spec", "PDP-13")]
    public async Task APathOverTwoHundredCharacters_Is400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await GetAsync($"{Products}?path=/{new string('a', 200)}")).Status);

    [Theory]
    [Trait("Spec", "PDP-13")]
    [InlineData("/girls/dresses/nothing-here")]
    [InlineData("/girls/dresses/../../etc/passwd")]
    [InlineData("%2Fgirls%2F..%2F..%2Fetc%2Fpasswd")]
    [InlineData("/<script>alert(1)</script>")]
    [InlineData("/a/b?x=1")]
    [InlineData("C:%5Cwindows%5Cwin.ini")]
    public async Task AnUnknownOrHostilePath_AnswersNotFound_WithNoEchoNoStackTraceAndNoCaching(string path)
    {
        var response = await api.Client.GetAsync($"{Products}?path={path}&colour=<b>&size={new string('x', 500)}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.Equal("NotFound", json.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("path").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("page").ValueKind);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("script", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body, StringComparison.Ordinal);
        Assert.DoesNotContain("quantity", body, StringComparison.OrdinalIgnoreCase);
    }
}
