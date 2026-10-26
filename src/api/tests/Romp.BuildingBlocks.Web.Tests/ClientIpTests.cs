using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Romp.BuildingBlocks.Web;

namespace Romp.BuildingBlocks.Web.Tests;

/// <summary>Sessions task 11 (AC-15): the visitor address is believed only when the proxy in front of the API says so.</summary>
public sealed class ClientIpTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Test";

        public string ContentRootPath { get; set; } = "/";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<HttpContext> RunAsync(string peer, Dictionary<string, string> headers, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRompHostOptions(configuration, new Env("Production"));
        services.AddRompForwardedHeaders();
        var provider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(provider);
        app.UseRompForwardedHeaders();
        app.Run(_ => Task.CompletedTask);

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "http";
        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        await app.Build()(context);
        return context;
    }

    private static (string, string) Trusted(string entry, int index = 0) => ($"Network:TrustedProxies:{index}", entry);

    [Fact]
    [Trait("Spec", "AC-15")]
    public async Task ForgedHeader_IsIgnoredFromUntrustedPeers()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Forwarded-For"] = "1.2.3.4",
            ["CF-Connecting-IP"] = "1.2.3.4",
            ["X-Forwarded-Proto"] = "https",
        };

        // The trusted proxy is 10.0.0.0/8; the caller is somebody else on the internet.
        var viaDefaultHeader = await RunAsync("203.0.113.9", headers, Trusted("10.0.0.0/8"));
        Assert.Equal("203.0.113.9", viaDefaultHeader.ClientIp());
        Assert.Equal("http", viaDefaultHeader.Request.Scheme); // the scheme is not believed either

        var viaCloudflareHeader = await RunAsync("203.0.113.9", headers, Trusted("10.0.0.0/8"), ("Network:ClientIpHeader", "CF-Connecting-IP"));
        Assert.Equal("203.0.113.9", viaCloudflareHeader.ClientIp());

        // With no trusted proxy configured at all, nothing forwarded is believed.
        var noProxies = await RunAsync("10.1.2.3", headers);
        Assert.Equal("10.1.2.3", noProxies.ClientIp());
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public async Task ATrustedProxy_ProvidesTheVisitorAddress()
    {
        var context = await RunAsync(
            "10.1.2.3",
            new() { ["X-Forwarded-For"] = "198.51.100.7", ["X-Forwarded-Proto"] = "https" },
            Trusted("10.0.0.0/8"));

        Assert.Equal("198.51.100.7", context.ClientIp());
        Assert.Equal("https", context.Request.Scheme);
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public async Task AnAddressTheVisitorAddedToTheChain_IsNotUsed()
    {
        // The visitor sent "X-Forwarded-For: 6.6.6.6"; the trusted proxy appended the address it actually saw.
        var context = await RunAsync("10.1.2.3", new() { ["X-Forwarded-For"] = "6.6.6.6, 198.51.100.7" }, Trusted("10.0.0.0/8"));

        Assert.Equal("198.51.100.7", context.ClientIp());
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public async Task TheCloudflareHeader_IsReadOnlyFromATrustedPeer_WhenConfigured()
    {
        var headers = new Dictionary<string, string> { ["CF-Connecting-IP"] = "198.51.100.8", ["X-Forwarded-For"] = "9.9.9.9" };
        var settings = new[] { Trusted("10.0.0.0/8"), ("Network:ClientIpHeader", "CF-Connecting-IP") };

        Assert.Equal("198.51.100.8", (await RunAsync("10.1.2.3", headers, settings)).ClientIp());
        Assert.Equal("203.0.113.9", (await RunAsync("203.0.113.9", headers, settings)).ClientIp());
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public async Task AGarbageHeader_FromATrustedPeer_FallsBackToThePeer()
    {
        var context = await RunAsync("10.1.2.3", new() { ["X-Forwarded-For"] = "not-an-ip, <script>" }, Trusted("10.0.0.0/8"));

        Assert.Equal("10.1.2.3", context.ClientIp());
    }

    [Fact]
    [Trait("Spec", "AC-15")]
    public async Task ClientIp_IsAlwaysTextThatFitsTheStoredColumn()
    {
        var mapped = new DefaultHttpContext();
        mapped.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.9");
        Assert.Equal("203.0.113.9", mapped.ClientIp()); // an IPv4 address seen over a dual-stack socket reads as IPv4

        var none = new DefaultHttpContext();
        Assert.Equal("unknown", none.ClientIp());

        var v6 = await RunAsync("2001:db8::1", new(), Trusted("10.0.0.0/8"));
        Assert.InRange(v6.ClientIp().Length, 1, 45);
    }

    [Theory]
    [Trait("Spec", "AC-15")]
    [InlineData("X-Real-IP")]
    [InlineData("Host")]
    [InlineData("")]
    public void OnlyKnownClientIpHeaders_AreAccepted(string header)
    {
        var result = new NetworkOptionsValidator().Validate(null, new NetworkOptions { ClientIpHeader = header });

        Assert.True(result.Failed);
    }

    [Theory]
    [Trait("Spec", "AC-15")]
    [InlineData("X-Forwarded-For")]
    [InlineData("CF-Connecting-IP")]
    public void TheTwoKnownHeaders_AreAccepted(string header)
    {
        Assert.True(new NetworkOptionsValidator().Validate(null, new NetworkOptions { ClientIpHeader = header }).Succeeded);
    }
}
