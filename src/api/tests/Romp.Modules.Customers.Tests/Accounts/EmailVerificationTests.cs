using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts S6 (ACT-27, ACT-28, ACT-31): the emailed verification link, resend and change of email.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class EmailVerificationTests(CustomersPostgres postgres)
{
    private static async Task<(string Email, long CustomerId)> RegisteredAsync(AccountsHarness harness)
    {
        var email = $"verify.{Guid.NewGuid():N}@example.test";
        var result = await harness.Send(RegisterTests.Register(email));
        return (email, result.Session!.CustomerId);
    }

    private static string LastToken(AccountsHarness harness, string kind, string to) =>
        harness.Mailer.Sent.Last(m => m.Kind == kind && m.To == to).Token;

    [Fact]
    [Trait("Spec", "ACT-27")]
    public async Task Register_EmailsAVerificationLink_AndTheLinkMarksTheEmailVerified_Once()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, customerId) = await RegisteredAsync(harness);
        var token = LastToken(harness, "verify", email);

        Assert.Equal(VerifyEmailResult.Verified, await harness.Send(new VerifyEmailCommand(token)));
        Assert.Equal(VerifyEmailResult.Invalid, await harness.Send(new VerifyEmailCommand(token)));

        var customer = await harness.Read(db => db.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId));
        Assert.True(customer.EmailIsVerified);
    }

    [Fact]
    [Trait("Spec", "ACT-27")]
    [Trait("Spec", "SEC-25")]
    public async Task TheLinkValue_IsStoredOnlyAsAHash()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, customerId) = await RegisteredAsync(harness);
        var token = LastToken(harness, "verify", email);

        var stored = await harness.Read(db => db.EmailVerificationTokens.AsNoTracking().SingleAsync(t => t.CustomerId == customerId));

        Assert.DoesNotContain(token, Convert.ToBase64String(stored.TokenHash), StringComparison.Ordinal);
        Assert.True(CustomerTokens.Matches(token, stored.TokenHash));
    }

    [Fact]
    [Trait("Spec", "ACT-27")]
    public async Task AnExpiredLink_IsInvalid_AndTheEmailStaysUnverified()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, customerId) = await RegisteredAsync(harness);
        var token = LastToken(harness, "verify", email);

        harness.Clock.Advance(EmailVerificationToken.Lifetime + TimeSpan.FromMinutes(1));

        Assert.Equal(VerifyEmailResult.Invalid, await harness.Send(new VerifyEmailCommand(token)));
        Assert.False((await harness.Read(db => db.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId))).EmailIsVerified);
    }

    [Theory]
    [Trait("Spec", "ACT-27")]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task AnUnknownLink_IsInvalid(string token)
    {
        using var harness = new AccountsHarness(postgres);

        Assert.Equal(VerifyEmailResult.Invalid, await harness.Send(new VerifyEmailCommand(token)));
    }

    [Fact]
    [Trait("Spec", "ACT-28")]
    public async Task Resend_SendsAFreshLink_AndTheOldLinkStopsWorking()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, customerId) = await RegisteredAsync(harness);
        var first = LastToken(harness, "verify", email);

        var status = await harness.Send(new SendEmailVerificationCommand(), customerId);
        var second = LastToken(harness, "verify", email);

        Assert.Equal(SendVerificationStatus.Sent, status);
        Assert.NotEqual(first, second);
        Assert.Equal(VerifyEmailResult.Invalid, await harness.Send(new VerifyEmailCommand(first)));
        Assert.Equal(VerifyEmailResult.Verified, await harness.Send(new VerifyEmailCommand(second)));
    }

    [Fact]
    [Trait("Spec", "ACT-28")]
    public async Task Resend_IsLimitedPerHour()
    {
        using var harness = new AccountsHarness(postgres);
        var (_, customerId) = await RegisteredAsync(harness);

        var results = new List<SendVerificationStatus>();
        for (var i = 0; i < EmailVerificationToken.MaxPerHour + 2; i++)
        {
            results.Add(await harness.Send(new SendEmailVerificationCommand(), customerId));
        }

        Assert.Equal(SendVerificationStatus.RateLimited, results[^1]);
        Assert.Contains(SendVerificationStatus.Sent, results);

        harness.Clock.Advance(TimeSpan.FromMinutes(61));
        Assert.Equal(SendVerificationStatus.Sent, await harness.Send(new SendEmailVerificationCommand(), customerId));
    }

    [Fact]
    [Trait("Spec", "ACT-31")]
    public async Task Resend_ForAVerifiedCustomer_SendsNothing()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, customerId) = await RegisteredAsync(harness);
        await harness.Send(new VerifyEmailCommand(LastToken(harness, "verify", email)));
        var before = harness.Mailer.Sent.Count;

        var status = await harness.Send(new SendEmailVerificationCommand(), customerId);

        Assert.Equal(SendVerificationStatus.AlreadyVerified, status);
        Assert.Equal(before, harness.Mailer.Sent.Count);
    }

    [Fact]
    [Trait("Spec", "NFR-FT-08")]
    public async Task Register_StillSucceeds_WhenTheMailServerIsDown()
    {
        using var harness = new AccountsHarness(postgres);
        harness.Mailer.Fail = true;

        var result = await harness.Send(RegisterTests.Register());

        Assert.Equal(RegisterStatus.Registered, result.Status);
        Assert.NotNull(result.Session);
    }

    [Fact]
    [Trait("Spec", "ACT-28")]
    public async Task ChangeEmail_MakesTheNewAddressUnverified_AndEmailsItsOwnLink()
    {
        using var harness = new AccountsHarness(postgres);
        var (oldEmail, customerId) = await RegisteredAsync(harness);
        await harness.Send(new VerifyEmailCommand(LastToken(harness, "verify", oldEmail)));
        var newEmail = $"moved.{Guid.NewGuid():N}@example.test";

        var result = await harness.Send(new ChangeEmailCommand(newEmail, new SecretString("Strong1Pass")), customerId);

        Assert.Equal(ChangeEmailStatus.Changed, result);
        var customer = await harness.Read(db => db.Customers.AsNoTracking().SingleAsync(c => c.Id == customerId));
        Assert.Equal(newEmail, customer.Email);
        Assert.False(customer.EmailIsVerified);
        Assert.Equal(VerifyEmailResult.Verified, await harness.Send(new VerifyEmailCommand(LastToken(harness, "verify", newEmail))));
    }

    [Fact]
    [Trait("Spec", "ACT-28")]
    public async Task ChangeEmail_ALinkSentToTheOldAddress_NoLongerVerifiesAnything()
    {
        using var harness = new AccountsHarness(postgres);
        var (oldEmail, customerId) = await RegisteredAsync(harness);
        var oldToken = LastToken(harness, "verify", oldEmail);

        await harness.Send(new ChangeEmailCommand($"moved.{Guid.NewGuid():N}@example.test", new SecretString("Strong1Pass")), customerId);

        Assert.Equal(VerifyEmailResult.Invalid, await harness.Send(new VerifyEmailCommand(oldToken)));
    }

    [Fact]
    [Trait("Spec", "ACT-28")]
    public async Task ChangeEmail_NeedsTheCurrentPassword_AndRefusesAnAddressInUse()
    {
        using var harness = new AccountsHarness(postgres);
        var (_, customerId) = await RegisteredAsync(harness);
        var (otherEmail, _) = await RegisteredAsync(harness);

        Assert.Equal(ChangeEmailStatus.WrongPassword,
            await harness.Send(new ChangeEmailCommand($"x.{Guid.NewGuid():N}@example.test", new SecretString("Wrong1Pass")), customerId));
        Assert.Equal(ChangeEmailStatus.EmailTaken,
            await harness.Send(new ChangeEmailCommand(otherEmail.ToUpperInvariant(), new SecretString("Strong1Pass")), customerId));
    }
}
