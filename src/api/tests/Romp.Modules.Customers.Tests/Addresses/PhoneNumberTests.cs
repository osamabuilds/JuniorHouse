using Romp.Modules.Customers.Domain;

namespace Romp.Modules.Customers.Tests.Addresses;

/// <summary>customers/addresses ADD-5: one stored form for a Pakistani phone number, whatever the customer typed (Q-ACT-7 default).</summary>
public sealed class PhoneNumberTests
{
    [Theory]
    [Trait("Spec", "ADD-5")]
    [InlineData("03001234567", "+923001234567")]
    [InlineData("0300 1234567", "+923001234567")]
    [InlineData("0300-123-4567", "+923001234567")]
    [InlineData("+92 300 1234567", "+923001234567")]
    [InlineData("+92-300-1234567", "+923001234567")]
    [InlineData("923001234567", "+923001234567")]
    [InlineData("00923001234567", "+923001234567")]
    [InlineData("(0300) 1234567", "+923001234567")]
    [InlineData("03001234567 ", "+923001234567")]
    public void Normalise_MobileNumbers_GiveOneForm(string typed, string stored)
    {
        Assert.Equal(stored, PhoneNumber.Normalise(typed));
    }

    [Theory]
    [Trait("Spec", "ADD-5")]
    [InlineData("042 35761234", "+924235761234")]
    [InlineData("021-34567890", "+922134567890")]
    [InlineData("+92 51 23456789", "+925123456789")]
    public void Normalise_LandlinesWithAnAreaCode_AreAccepted(string typed, string stored)
    {
        Assert.Equal(stored, PhoneNumber.Normalise(typed));
    }

    [Theory]
    [Trait("Spec", "ADD-5")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0300123456")]
    [InlineData("030012345678")]
    [InlineData("0100 1234567")]
    [InlineData("+1 415 555 0100")]
    [InlineData("0300-ABC-4567")]
    [InlineData("0300 1234567 ext 5")]
    [InlineData("+92300123456789")]
    public void Normalise_Rejects_ShortLongForeignOrNonNumeric(string typed)
    {
        Assert.Throws<ArgumentException>(() => PhoneNumber.Normalise(typed));
        Assert.False(PhoneNumber.TryNormalise(typed, out _));
    }

    [Fact]
    [Trait("Spec", "ADD-5")]
    public void Normalise_IsIdempotent()
    {
        var once = PhoneNumber.Normalise("0300 1234567");

        Assert.Equal(once, PhoneNumber.Normalise(once));
    }
}
