using Romp.BuildingBlocks.Domain;
using Romp.Modules.Warehouse.Domain.Bins;

namespace Romp.Modules.Warehouse.Tests.Bins;

public sealed class BinTests
{
    [Theory]
    [InlineData("a", "3", "2", "A-3-2")]
    [InlineData("AB", "03", "2", "AB-03-2")]
    [InlineData(" zon ", " 123 ", " 1b ", "ZON-123-1B")]
    [Trait("Spec", "BIN-1")]
    public void TheCode_IsBuiltFromZoneAisleAndShelf_UpperCased(string zone, string aisle, string shelf, string expected)
    {
        var bin = new Bin(zone, aisle, shelf);

        Assert.Equal(expected, bin.BinCode);
        Assert.True(bin.IsActive);
    }

    [Theory]
    [InlineData("", "03", "2")]
    [InlineData("ABCD", "03", "2")]
    [InlineData("A", "", "2")]
    [InlineData("A", "1234", "2")]
    [InlineData("A", "0X", "2")]
    [InlineData("A", "03", "")]
    [InlineData("A", "03", "ABC")]
    [InlineData("A-", "03", "2")]
    [InlineData("A'; DROP", "03", "2")]
    [InlineData("<b>", "03", "2")]
    [InlineData("A", "03", "é")]
    [Trait("Spec", "BIN-3")]
    public void APartOutsideThePattern_IsRefused(string zone, string aisle, string shelf) =>
        Assert.Throws<DomainException>(() => new Bin(zone, aisle, shelf));

    [Fact]
    [Trait("Spec", "BIN-4")]
    public void ABinWithStock_CannotBeDeactivated()
    {
        var bin = new Bin("A", "03", "2");

        var ex = Assert.Throws<DomainException>(() => bin.SetActive(false, unitsOnHand: 4));

        Assert.Contains("holds stock", ex.Message, StringComparison.Ordinal);
        Assert.True(bin.IsActive);
    }

    [Fact]
    [Trait("Spec", "BIN-4")]
    public void AnEmptyBin_CanBeDeactivated_AndReactivated()
    {
        var bin = new Bin("A", "03", "2");

        bin.SetActive(false, unitsOnHand: 0);
        Assert.False(bin.IsActive);

        bin.SetActive(true, unitsOnHand: 0);
        Assert.True(bin.IsActive);
    }

    [Fact]
    [Trait("Spec", "BIN-4")]
    public void SettingTheCurrentState_IsANoOp()
    {
        var bin = new Bin("A", "03", "2");

        bin.SetActive(true, unitsOnHand: 9);

        Assert.True(bin.IsActive);
    }

    [Fact]
    [Trait("Spec", "BIN-4")]
    public void ReactivatingAnInactiveBin_IsAlwaysAllowed()
    {
        var bin = new Bin("A", "03", "2");
        bin.SetActive(false, unitsOnHand: 0);

        bin.SetActive(true, unitsOnHand: 0);

        Assert.True(bin.IsActive);
    }
}
