using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Romp.BuildingBlocks.Web;

namespace Romp.BuildingBlocks.Web.Tests;

/// <summary>Sessions task 9 (AC-1, AC-14): cookies without the Secure flag exist only for local development over HTTP.</summary>
public sealed class IdentityOptionsValidatorTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Test";

        public string ContentRootPath { get; set; } = "/";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static ValidateOptionsResult Validate(string environment, bool allowInsecureCookies) =>
        new IdentityOptionsValidator(new Env(environment)).Validate(null, new IdentityOptions { AllowInsecureCookies = allowInsecureCookies });

    [Theory]
    [Trait("Spec", "AC-1")]
    [Trait("Spec", "AC-14")]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("anything-else")]
    public void InsecureCookies_AreRefusedInProduction(string environment)
    {
        var result = Validate(environment, allowInsecureCookies: true);

        Assert.True(result.Failed);
        Assert.Contains("Identity:AllowInsecureCookies", result.FailureMessage, StringComparison.Ordinal);
        Assert.True(Validate(environment, allowInsecureCookies: false).Succeeded);
    }

    [Theory]
    [Trait("Spec", "AC-1")]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void InsecureCookies_AreAllowedInDevelopmentAndTesting(string environment)
    {
        Assert.True(Validate(environment, allowInsecureCookies: true).Succeeded);
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public void Cookies_AreSecureByDefault()
    {
        Assert.False(new IdentityOptions().AllowInsecureCookies);
    }
}
