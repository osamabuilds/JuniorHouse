using Romp.Modules.Warehouse.Application.Abstractions;
using Romp.Modules.Warehouse.Infrastructure;

namespace Romp.Modules.Warehouse.Tests.Support;

/// <summary>Plan G4: bad configuration (a zero adjustment limit, a negative threshold) must stop the host, not reach the warehouse floor.</summary>
public sealed class WarehouseOptionsValidatorTests
{
    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(Action<WarehouseOptions>? configure = null)
    {
        var options = new WarehouseOptions();
        configure?.Invoke(options);
        return new WarehouseOptionsValidator().Validate(null, options);
    }

    [Fact]
    [Trait("Spec", "ADJ-4b")]
    [Trait("Spec", "ALR-6")]
    public void TheDefaults_AreValid_AndMatchTheOwnerDecisions()
    {
        var options = new WarehouseOptions();

        Assert.Equal(20, options.LargeAdjustmentUnits);
        Assert.Equal(5, options.DefaultLowStockThreshold);
        Assert.True(Validate().Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100_001)]
    [Trait("Spec", "ADJ-4b")]
    public void ALargeAdjustmentLimit_OutsideTheRange_IsRefused(int limit)
    {
        var result = Validate(o => o.LargeAdjustmentUnits = limit);

        Assert.True(result.Failed);
        Assert.Contains("LargeAdjustmentUnits", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_001)]
    [Trait("Spec", "ALR-6")]
    public void ADefaultThreshold_OutsideTheRange_IsRefused(int threshold)
    {
        var result = Validate(o => o.DefaultLowStockThreshold = threshold);

        Assert.True(result.Failed);
        Assert.Contains("DefaultLowStockThreshold", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "ALR-6")]
    public void AThresholdOfZero_IsAllowed_AlertOnlyAtStockOut() =>
        Assert.True(Validate(o => o.DefaultLowStockThreshold = 0).Succeeded);
}
