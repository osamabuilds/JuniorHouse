using Microsoft.Extensions.Options;
using Romp.Modules.Catalog.Application.Abstractions;
using Romp.Modules.Catalog.Infrastructure;

namespace Romp.Modules.Catalog.Tests.Support;

/// <summary>Plan G7: bad configuration (a zero image limit, an unknown type, a reserved slug that is not a slug) stops the host.</summary>
public sealed class CatalogOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(Action<CatalogOptions>? configure = null)
    {
        var options = new CatalogOptions();
        configure?.Invoke(options);
        return new CatalogOptionsValidator().Validate(null, options);
    }

    [Fact]
    [Trait("Spec", "Q-MED-2")]
    public void TheDefaults_AreValid_AndMatchTheOwnersProposal()
    {
        var options = new CatalogOptions();

        Assert.Equal(5 * 1024 * 1024, options.MaxImageBytes);
        Assert.Equal(10, options.MaxImagesPerProduct);
        Assert.Equal(["jpeg", "png", "webp"], options.AllowedImageTypes);
        Assert.Equal(80, options.MaxSlugLength);
        Assert.Contains("cart", options.ReservedSlugs);
        Assert.True(Validate().Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(26 * 1024 * 1024)]
    public void AnImageLimitOutsideTheRange_IsRefused(int bytes)
    {
        var result = Validate(o => o.MaxImageBytes = bytes);

        Assert.True(result.Failed);
        Assert.Contains("MaxImageBytes", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void AnImageCountOutsideTheRange_IsRefused(int count) =>
        Assert.Contains("MaxImagesPerProduct", Validate(o => o.MaxImagesPerProduct = count).FailureMessage, StringComparison.Ordinal);

    [Fact]
    public void AnUnknownOrEmptyImageTypeList_IsRefused()
    {
        Assert.True(Validate(o => o.AllowedImageTypes = ["gif"]).Failed);
        Assert.True(Validate(o => o.AllowedImageTypes = []).Failed);
        Assert.True(Validate(o => o.AllowedImageTypes = ["png"]).Succeeded);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(81)]
    public void ASlugLengthOutsideWhatTheColumnHolds_IsRefused(int length) =>
        Assert.Contains("MaxSlugLength", Validate(o => o.MaxSlugLength = length).FailureMessage, StringComparison.Ordinal);

    [Fact]
    public void AReservedSlugThatIsNotASlug_IsRefused()
    {
        Assert.True(Validate(o => o.ReservedSlugs = ["Cart"]).Failed);
        Assert.True(Validate(o => o.ReservedSlugs = ["a/b"]).Failed);
        Assert.True(Validate(o => o.ReservedSlugs = ["cart", "check-out"]).Succeeded);
    }
}
