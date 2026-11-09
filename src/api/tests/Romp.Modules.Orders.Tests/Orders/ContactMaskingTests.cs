using Romp.Modules.Orders.Contracts;

namespace Romp.Modules.Orders.Tests.Orders;

public sealed class ContactMaskingTests
{
    [Theory]
    [Trait("Spec", "DLG-6")]
    [Trait("Spec", "SEC-25")]
    [InlineData("ayesha@mail.com", "a***@mail.com")]
    [InlineData("A@x.pk", "A***@x.pk")]
    [InlineData("not-an-address", "***")]
    public void MaskEmail_KeepsTheFirstLetterAndTheDomainOnly(string email, string expected) =>
        Assert.Equal(expected, ContactMasking.MaskEmail(email));

    [Fact]
    [Trait("Spec", "DLG-6")]
    public void MaskEmail_WhenThereIsNone_IsNull()
    {
        Assert.Null(ContactMasking.MaskEmail(null));
        Assert.Null(ContactMasking.MaskEmail("  "));
    }

    [Theory]
    [Trait("Spec", "DLG-6")]
    [Trait("Spec", "SEC-25")]
    [InlineData("+923001234567", "03XX-XXX4567")]
    [InlineData("03001234567", "03XX-XXX4567")]
    [InlineData("+92 300 1234567", "03XX-XXX4567")]
    [InlineData("12345", "XXXX2345")]
    [InlineData("", "XXXX")]
    public void MaskMobile_ShowsTheNetworkPrefixAndTheLastFourDigits(string mobile, string expected) =>
        Assert.Equal(expected, ContactMasking.MaskMobile(mobile));
}
