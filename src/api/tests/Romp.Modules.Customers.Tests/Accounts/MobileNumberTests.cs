using Romp.BuildingBlocks.Domain;
using Romp.Modules.Customers.Domain;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts ACT-29 and Q-ACT-7: sign-up takes a Pakistani mobile (+92 3xx xxxxxxx) only; a landline is refused.</summary>
public sealed class MobileNumberTests
{
    [Theory]
    [Trait("Spec", "ACT-29")]
    [InlineData("0300 1234567", "+923001234567")]
    [InlineData("+92 345 1234567", "+923451234567")]
    [InlineData("923211234567", "+923211234567")]
    public void TryNormaliseMobile_PakistaniMobiles_GiveOneForm(string typed, string stored)
    {
        Assert.True(PhoneNumber.TryNormaliseMobile(typed, out var normalised));
        Assert.Equal(stored, normalised);
    }

    [Theory]
    [Trait("Spec", "ACT-29")]
    [InlineData("042 35761234")]
    [InlineData("021-34567890")]
    [InlineData("+92 51 23456789")]
    [InlineData("0200 1234567")]
    [InlineData("12345")]
    [InlineData("")]
    [InlineData(null)]
    public void TryNormaliseMobile_LandlinesAndJunk_AreRefused(string? typed)
    {
        Assert.False(PhoneNumber.TryNormaliseMobile(typed, out var normalised));
        Assert.Equal(string.Empty, normalised);
    }
}
