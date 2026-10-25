using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;

namespace Romp.BuildingBlocks.Web.Tests;

/// <summary>G7: Site, Network and Identity host options are validated at start-up.</summary>
public sealed class HostOptionsTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static ValidateOptionsResult Site(string environment, string? url) =>
        new SiteOptionsValidator(new Env(environment)).Validate(null, new SiteOptions { PublicBaseUrl = url });

    private static ValidateOptionsResult Network(params string[] proxies) =>
        new NetworkOptionsValidator().Validate(null, new NetworkOptions { TrustedProxies = proxies });

    private static ValidateOptionsResult Identity(string environment, bool? useReal) =>
        new IdentityOptionsValidator(new Env(environment)).Validate(null, new IdentityOptions { UseRealAuthentication = useReal });

    // ---- Site:PublicBaseUrl -------------------------------------------------------------

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Site_OutsideDevelopment_RequiresHttps(string environment)
    {
        Assert.True(Site(environment, "https://staging.example.org").Succeeded);
        Assert.True(Site(environment, "http://staging.example.org").Failed);
        Assert.True(Site(environment, null).Failed);
        Assert.True(Site(environment, "  ").Failed);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Site_InDevelopmentAndTesting_AllowsHttpOrNothing(string environment)
    {
        Assert.True(Site(environment, "http://localhost:4000").Succeeded);
        Assert.True(Site(environment, null).Succeeded);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.org")]
    [InlineData("https://example.org/shop")]
    [InlineData("https://example.org?x=1")]
    [InlineData("https://example.org#frag")]
    [InlineData("https://user:pw@example.org")]
    public void Site_RejectsAnythingButABareOrigin(string url)
    {
        Assert.True(Site("Production", url).Failed);
        Assert.True(Site("Development", url).Failed);
    }

    // ---- Network:TrustedProxies ---------------------------------------------------------

    [Fact]
    public void Network_AcceptsAddressesAndNetworks_AndNothing()
    {
        Assert.True(Network().Succeeded);
        Assert.True(Network("10.0.0.5", "172.18.0.0/16", "::1", "fd00::/64").Succeeded);
    }

    [Theory]
    [InlineData("example.org")]
    [InlineData("10.0.0.256")]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.0/x")]
    [InlineData("")]
    public void Network_RejectsEntriesThatAreNotIpOrCidr(string entry)
    {
        Assert.True(Network(entry).Failed);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("0.0.0.0/1")]
    [InlineData("0.0.0.0")]
    public void Network_RejectsTrustingEveryone(string entry)
    {
        Assert.True(Network(entry).Failed);
    }

    // ---- Identity:UseRealAuthentication -------------------------------------------------

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Identity_OutsideDevelopment_IsAlwaysReal_AndCannotBeTurnedOff(string environment)
    {
        var env = new Env(environment);

        Assert.True(IdentityOptions.IsRealAuthentication(new IdentityOptions { UseRealAuthentication = null }, env));
        Assert.True(IdentityOptions.IsRealAuthentication(new IdentityOptions { UseRealAuthentication = true }, env));
        Assert.True(Identity(environment, false).Failed);
        Assert.True(Identity(environment, null).Succeeded);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Identity_InDevelopmentAndTesting_DefaultsToTheDevelopmentHandler_AndCanBeSwitchedOn(string environment)
    {
        var env = new Env(environment);

        Assert.False(IdentityOptions.IsRealAuthentication(new IdentityOptions(), env));
        Assert.True(IdentityOptions.IsRealAuthentication(new IdentityOptions { UseRealAuthentication = true }, env));
        Assert.True(Identity(environment, false).Succeeded);
    }

    // ---- start-up -----------------------------------------------------------------------

    private static IHost HostWith(string environment, Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddRompHostOptions(builder.Configuration, builder.Environment);
        return builder.Build();
    }

    [Fact]
    public async Task StartUp_InProductionWithAnHttpBaseUrl_Fails()
    {
        using var host = HostWith("Production", new() { ["Site:PublicBaseUrl"] = "http://staging.example.org" });

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task StartUp_InProductionWithTrustEveryoneProxies_Fails()
    {
        using var host = HostWith("Production", new()
        {
            ["Site:PublicBaseUrl"] = "https://staging.example.org",
            ["Network:TrustedProxies:0"] = "0.0.0.0/0",
        });

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task StartUp_InProductionWithValidSettings_Succeeds()
    {
        using var host = HostWith("Production", new()
        {
            ["Site:PublicBaseUrl"] = "https://staging.example.org",
            ["Network:TrustedProxies:0"] = "172.18.0.0/16",
        });

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task StartUp_InDevelopmentWithNothingConfigured_Succeeds()
    {
        using var host = HostWith("Development", []);

        await host.StartAsync();
        await host.StopAsync();
    }
}
