using Romp.Modules.Customers.Domain.Accounts;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts ACT-1, ACT-27, ACT-28, ACT-29: the customer holds a password hash, a mobile saved unverified, and an email that starts unverified.</summary>
public sealed class CustomerCredentialsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private static Customer NewCustomer() => Customer.Register(" Parent@Example.com ", "Ayesha", "Khan", "0300 1234567", "hash-1");

    [Fact]
    [Trait("Spec", "ACT-1")]
    public void Register_NormalisesTheEmail_StoresTheMobileInOneForm_AndStartsUnverified()
    {
        var customer = NewCustomer();

        Assert.Equal("parent@example.com", customer.Email);
        Assert.Equal("+923001234567", customer.Phone);
        Assert.Equal("hash-1", customer.PasswordHash);
        Assert.True(customer.HasPassword);
        Assert.Null(customer.EmailVerifiedAt);
        Assert.False(customer.EmailIsVerified);
    }

    [Theory]
    [Trait("Spec", "ACT-29")]
    [InlineData("042 35761234")]
    [InlineData("")]
    [InlineData(null)]
    public void Register_AMobileThatIsNotAPakistaniMobile_IsRefused(string? phone)
    {
        Assert.Throws<ArgumentException>(() => Customer.Register("a@example.com", "A", "B", phone, "hash"));
    }

    [Fact]
    [Trait("Spec", "ACT-1")]
    public void Register_WithoutAPasswordHash_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => Customer.Register("a@example.com", "A", "B", "03001234567", " "));
    }

    [Fact]
    [Trait("Spec", "ACT-27")]
    public void MarkEmailVerified_OnlyForTheCurrentAddress_AndOnlyOnce()
    {
        var customer = NewCustomer();

        Assert.False(customer.MarkEmailVerified("other@example.com", Now));
        Assert.True(customer.MarkEmailVerified("PARENT@example.com", Now));
        Assert.Equal(Now, customer.EmailVerifiedAt);
        Assert.True(customer.MarkEmailVerified("parent@example.com", Now.AddHours(1)));
        Assert.Equal(Now, customer.EmailVerifiedAt);
    }

    [Fact]
    [Trait("Spec", "ACT-28")]
    public void ChangeEmail_MakesTheNewAddressUnverified()
    {
        var customer = NewCustomer();
        customer.MarkEmailVerified("parent@example.com", Now);

        customer.ChangeEmail(" New@Example.com ");

        Assert.Equal("new@example.com", customer.Email);
        Assert.Null(customer.EmailVerifiedAt);
    }

    [Fact]
    [Trait("Spec", "ACT-28")]
    public void ChangeEmail_ToTheSameAddress_KeepsItVerified()
    {
        var customer = NewCustomer();
        customer.MarkEmailVerified("parent@example.com", Now);

        customer.ChangeEmail("PARENT@example.com");

        Assert.Equal(Now, customer.EmailVerifiedAt);
    }

    [Fact]
    [Trait("Spec", "ACT-7")]
    public void SetPasswordHash_ReplacesTheOldHash()
    {
        var customer = NewCustomer();

        customer.SetPasswordHash("hash-2");

        Assert.Equal("hash-2", customer.PasswordHash);
        Assert.Throws<ArgumentException>(() => customer.SetPasswordHash(""));
    }

    [Fact]
    [Trait("Spec", "ACT-4")]
    public void RecordSignIn_StoresTheTime()
    {
        var customer = NewCustomer();

        customer.RecordSignIn(Now);

        Assert.Equal(Now, customer.LastSignedInAt);
    }

    [Fact]
    [Trait("Spec", "ACT-14")]
    public void AnAccountMadeWithCreate_HasNoPassword_SoItCannotSignInByPassword()
    {
        var customer = Customer.Create("dev@example.test", "Dev", "Customer");

        Assert.False(customer.HasPassword);
    }
}
