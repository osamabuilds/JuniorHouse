using Microsoft.EntityFrameworkCore;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts S5 (ACT-1 to ACT-3, ACT-29): registration in one step.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class RegisterTests(CustomersPostgres postgres)
{
    internal static RegisterCommand Register(string? email = null, string password = "Strong1Pass", string mobile = "0300 1234567") =>
        new(email ?? $"new.{Guid.NewGuid():N}@example.test", "Ayesha", "Khan", mobile, new SecretString(password), false, "203.0.113.5", null, null);

    [Fact]
    [Trait("Spec", "ACT-1")]
    [Trait("Spec", "TC-ACC-01")]
    [Trait("Spec", "ACT-29")]
    public async Task Register_ValidDetails_CreatesTheCustomer_AndSignsThemIn()
    {
        using var harness = new AccountsHarness(postgres);
        var email = $"Ayesha.{Guid.NewGuid():N}@Example.Test";

        var result = await harness.Send(Register(email));

        Assert.Equal(RegisterStatus.Registered, result.Status);
        Assert.NotNull(result.Session);
        Assert.NotEmpty(result.Session!.Tokens.Access.Reveal());
        var stored = await harness.Read(db => db.Customers.AsNoTracking().SingleAsync(c => c.Id == result.Session.CustomerId));
        Assert.Equal(email.ToLowerInvariant(), stored.Email);
        Assert.Equal("+923001234567", stored.Phone);
        Assert.False(stored.EmailIsVerified);
        Assert.StartsWith("$argon2id$", stored.PasswordHash, StringComparison.Ordinal);
        Assert.DoesNotContain("Strong1Pass", stored.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(1, await harness.Read(db => db.Sessions.CountAsync(s => s.CustomerId == stored.Id)));
    }

    [Theory]
    [Trait("Spec", "ACT-2")]
    [Trait("Spec", "TC-ACC-02")]
    [InlineData("short1A", PasswordRule.Length)]
    [InlineData("alllowercase1", PasswordRule.Capital)]
    [InlineData("ALLUPPERCASE1", PasswordRule.Small)]
    [InlineData("NoDigitsHere", PasswordRule.Digit)]
    [InlineData("Has Space1a", PasswordRule.NoSpaces)]
    public async Task Register_WeakPassword_NamesTheBrokenRule_AndCreatesNothing(string password, string broken)
    {
        using var harness = new AccountsHarness(postgres);
        var command = Register(password: password);
        var normalised = command.Email.ToLowerInvariant();

        var result = await harness.Send(command);

        Assert.Equal(RegisterStatus.PasswordRejected, result.Status);
        Assert.Equal([broken], result.UnmetRules);
        Assert.Null(result.Session);
        Assert.False(await harness.Read(db => db.Customers.AnyAsync(c => c.Email == normalised)));
    }

    [Fact]
    [Trait("Spec", "ACT-3")]
    [Trait("Spec", "TC-ACC-03")]
    public async Task Register_AnEmailAlreadyUsed_IgnoringCaseAndSpaces_IsRefused_AndNoSecondAccountExists()
    {
        using var harness = new AccountsHarness(postgres);
        var email = $"dup.{Guid.NewGuid():N}@example.test";
        await harness.Send(Register(email));

        var again = await harness.Send(Register("  " + email.ToUpperInvariant() + " "));

        Assert.Equal(RegisterStatus.EmailTaken, again.Status);
        Assert.Null(again.Session);
        Assert.Equal(1, await harness.Read(db => db.Customers.CountAsync(c => c.Email == email)));
    }

    [Theory]
    [Trait("Spec", "ACT-29")]
    [InlineData("12345")]
    [InlineData("021 1234567")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task Register_ANonMobileNumber_FailsValidation(string mobile)
    {
        using var harness = new AccountsHarness(postgres);

        var error = await Assert.ThrowsAsync<ValidationException>(() => harness.Send(Register(mobile: mobile)));

        Assert.Contains(nameof(RegisterCommand.Mobile), error.Errors.Keys);
    }

    [Theory]
    [Trait("Spec", "ACT-1")]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task Register_ABadEmail_FailsValidation(string email)
    {
        using var harness = new AccountsHarness(postgres);

        var error = await Assert.ThrowsAsync<ValidationException>(() => harness.Send(Register(email)));

        Assert.Contains(nameof(RegisterCommand.Email), error.Errors.Keys);
    }
}
