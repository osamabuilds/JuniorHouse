using Microsoft.Extensions.Options;
using Romp.Modules.Storefront.Infrastructure;

namespace Romp.Modules.Storefront.Tests.PublishedCatalog;

/// <summary>Published-catalog task 20: bad image or job settings stop the host at start-up rather than failing derivatives later.</summary>
public sealed class StorefrontOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(Action<StorefrontOptions>? configure = null)
    {
        var options = new StorefrontOptions();
        configure?.Invoke(options);
        return new StorefrontOptionsValidator().Validate(null, options);
    }

    private static void AssertRefused(ValidateOptionsResult result, string setting)
    {
        Assert.True(result.Failed);
        Assert.Contains(setting, result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "PCT-3")]
    [Trait("Spec", "PCT-7")]
    public void TheDefaults_AreValid() => Assert.True(Validate().Succeeded);

    [Theory]
    [InlineData(-1)]
    [InlineData(100_001)]
    [Trait("Spec", "PCT-3")]
    public void ALowStockThreshold_OutsideTheRange_IsRefused(int threshold) =>
        AssertRefused(Validate(o => o.LowStockThreshold = threshold), "LowStockThreshold");

    [Fact]
    [Trait("Spec", "PCT-3")]
    public void AThresholdOfZero_IsAllowed() => Assert.True(Validate(o => o.LowStockThreshold = 0).Succeeded);

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void NoImageWidths_IsRefused() => AssertRefused(Validate(o => o.ImageWidths = []), "ImageWidths");

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(4097)]
    [Trait("Spec", "PCT-7")]
    public void AnImageWidth_OutsideTheRange_IsRefused(int width) =>
        AssertRefused(Validate(o => o.ImageWidths = [320, width]), "ImageWidths");

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void ARepeatedImageWidth_IsRefused() => AssertRefused(Validate(o => o.ImageWidths = [320, 320]), "ImageWidths");

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void TooManyImageWidths_IsRefused() =>
        AssertRefused(Validate(o => o.ImageWidths = [100, 200, 300, 400, 500, 600, 700, 800, 900]), "ImageWidths");

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [Trait("Spec", "PCT-7")]
    public void AnImageQuality_OutsideTheRange_IsRefused(int quality) =>
        AssertRefused(Validate(o => o.ImageQuality = quality), "ImageQuality");

    [Theory]
    [InlineData(0L)]
    [InlineData(200_000_001L)]
    [Trait("Spec", "PCT-7")]
    public void APixelCap_OutsideTheRange_IsRefused(long pixels) =>
        AssertRefused(Validate(o => o.MaxImagePixels = pixels), "MaxImagePixels");

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    [Trait("Spec", "PCT-7")]
    public void AnAttemptCap_OutsideTheRange_IsRefused(int attempts) =>
        AssertRefused(Validate(o => o.MaxDerivativeAttempts = attempts), "MaxDerivativeAttempts");

    [Fact]
    [Trait("Spec", "PCT-5")]
    public void AZeroReconcileInterval_IsRefused() =>
        AssertRefused(Validate(o => o.ReconcileInterval = TimeSpan.Zero), "ReconcileInterval");

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void ANegativeRetryDelay_IsRefused() =>
        AssertRefused(Validate(o => o.DerivativeRetryDelay = TimeSpan.FromSeconds(-1)), "DerivativeRetryDelay");

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void AZeroPollInterval_IsRefused() =>
        AssertRefused(Validate(o => o.ImageWorkerPollInterval = TimeSpan.Zero), "ImageWorkerPollInterval");

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void AZeroCacheLifetime_IsRefused() =>
        AssertRefused(Validate(o => o.DerivativeCacheLifetime = TimeSpan.Zero), "DerivativeCacheLifetime");

    [Fact]
    [Trait("Spec", "PCT-7")]
    public void SeveralBadSettings_AreAllReported()
    {
        var result = Validate(o =>
        {
            o.ImageQuality = 0;
            o.MaxDerivativeAttempts = 0;
        });

        Assert.Contains("ImageQuality", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("MaxDerivativeAttempts", result.FailureMessage, StringComparison.Ordinal);
    }
}
