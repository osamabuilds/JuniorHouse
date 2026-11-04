using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;

namespace Romp.BuildingBlocks.Web.Tests;

public sealed class OriginCheckFilterTests
{
    private sealed class Filter(string? site, string? fallback) : OriginCheckFilterBase(Options.Create(new SiteOptions { PublicBaseUrl = site }))
    {
        protected override string? FallbackBaseUrl => fallback;
    }

    private static async Task<int> Status(Filter filter, string method, string? origin, string host = "api.local:8080")
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.Scheme = "http";
        http.Request.Host = new HostString(host);
        if (origin is not null)
        {
            http.Request.Headers["Origin"] = origin;
        }

        var result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(http),
            _ => ValueTask.FromResult<object?>(Results.Ok()));
        return result is IStatusCodeHttpResult status ? status.StatusCode ?? 200 : 200;
    }

    [Theory]
    [Trait("Spec", "SEC-09")]
    [InlineData("http://store.local:4000", true)]
    [InlineData("HTTP://STORE.LOCAL:4000", true)]
    [InlineData("http://evil.example", false)]
    [InlineData("http://api.local:8080", false)]
    [InlineData("null", false)]
    [InlineData("http://store.local:4000/path", false)]
    public async Task Post_AcceptsOnlyTheConfiguredSiteOrigin(string origin, bool allowed)
    {
        var status = await Status(new Filter("http://store.local:4000", null), "POST", origin);

        Assert.Equal(allowed ? 200 : 403, status);
    }

    [Fact]
    [Trait("Spec", "SEC-09")]
    public async Task Post_UsesTheModuleFallback_WhenNoSiteAddressIsConfigured()
    {
        Assert.Equal(200, await Status(new Filter(null, "http://store.local:4000"), "POST", "http://store.local:4000"));
        Assert.Equal(403, await Status(new Filter(null, "http://store.local:4000"), "POST", "http://api.local:8080"));
    }

    [Fact]
    [Trait("Spec", "SEC-09")]
    public async Task Post_UsesTheRequestOrigin_WhenNothingIsConfigured()
    {
        Assert.Equal(200, await Status(new Filter(null, null), "POST", "http://api.local:8080"));
        Assert.Equal(403, await Status(new Filter(null, null), "POST", "http://other.local"));
    }

    [Theory]
    [Trait("Spec", "SEC-09")]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task SafeMethods_AreNeverChecked(string method)
    {
        Assert.Equal(200, await Status(new Filter("http://store.local:4000", null), method, "http://evil.example"));
    }

    [Fact]
    [Trait("Spec", "SEC-09")]
    public async Task APostWithNoOriginHeader_IsLeftToTheOtherDefences()
    {
        Assert.Equal(200, await Status(new Filter("http://store.local:4000", null), "POST", null));
    }
}
