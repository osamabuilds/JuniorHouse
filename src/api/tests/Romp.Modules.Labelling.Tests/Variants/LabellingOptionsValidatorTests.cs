using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Romp.Modules.Labelling.Application.Abstractions;
using Romp.Modules.Labelling.Infrastructure;

namespace Romp.Modules.Labelling.Tests.Variants;

/// <summary>VAR-11 and ADR 0011: dangerous configuration must stop the host from starting, not reach real labels.</summary>
public sealed class LabellingOptionsValidatorTests
{
    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Romp.Api";

        public string ContentRootPath { get; set; } = "/";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(string environment, Action<LabellingOptions>? configure = null)
    {
        var options = new LabellingOptions();
        configure?.Invoke(options);
        return new LabellingOptionsValidator(new FakeEnvironment(environment)).Validate(null, options);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    [InlineData("Staging")]
    public void TheTestPrefixAndFakePrinters_AreFineOutsideProduction(string environment) =>
        Assert.True(Validate(environment, o => o.AllowFakePrinters = true).Succeeded);

    [Fact]
    [Trait("Spec", "VAR-11")]
    public void TheTestPrefix_IsRefusedInProduction()
    {
        var result = Validate("Production", o => o.CompanyPrefix = "0200000");

        Assert.True(result.Failed);
        Assert.Contains("test prefix", string.Join(' ', result.Failures!), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Spec", "VAR-11")]
    public void ARealPrefix_IsAcceptedInProduction() =>
        Assert.True(Validate("Production", o => o.CompanyPrefix = "6271234").Succeeded);

    [Fact]
    [Trait("Spec", "PJ-12")]
    public void FakePrinters_AreRefusedInProduction()
    {
        var result = Validate("Production", o =>
        {
            o.CompanyPrefix = "6271234";
            o.AllowFakePrinters = true;
        });

        Assert.True(result.Failed);
        Assert.Contains("AllowFakePrinters", string.Join(' ', result.Failures!), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567890")]
    [InlineData("abc1234")]
    [InlineData("")]
    [Trait("Spec", "VAR-11")]
    public void ABadPrefix_IsRefusedEverywhere(string prefix) =>
        Assert.True(Validate("Development", o => o.CompanyPrefix = prefix).Failed);

    [Fact]
    public void NonsenseLimits_AreRefused()
    {
        Assert.True(Validate("Development", o => o.SparePercent = -1).Failed);
        Assert.True(Validate("Development", o => o.SparePercent = 80).Failed);
        Assert.True(Validate("Development", o => o.MaxTagsPerJob = 0).Failed);
        Assert.True(Validate("Development", o => o.MaxTagsPerJob = 1_000_000).Failed);
        Assert.True(Validate("Development", o => o.MaxAttempts = 0).Failed);
        Assert.True(Validate("Development", o => o.BackoffSeconds = [10, -1]).Failed);
    }
}
