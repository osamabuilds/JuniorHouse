using Romp.BuildingBlocks.Web.Captcha;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Infrastructure.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts S6 (ACT-6, ACT-7, ACT-8, ACT-13): forgotten password, reset link, change of password.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class PasswordResetTests(CustomersPostgres postgres)
{
    private readonly string _ip = $"198.18.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

    private SignInCommand SignIn(string email, string password) =>
        new(email, new SecretString(password), false, FakeCaptchaVerifier.PassToken, _ip, null, "TestBrowser/1.0");

    private static async Task<(string Email, long CustomerId)> RegisteredAsync(AccountsHarness harness)
    {
        var email = $"reset.{Guid.NewGuid():N}@example.test";
        var result = await harness.Send(RegisterTests.Register(email));
        return (email, result.Session!.CustomerId);
    }

    private static string LastReset(AccountsHarness harness, string to) =>
        harness.Mailer.Sent.Last(m => m.Kind == "reset" && m.To == to).Token;

    [Fact]
    [Trait("Spec", "ACT-6")]
    [Trait("Spec", "TC-ACC-06")]
    public async Task Request_ForAKnownEmail_SendsALink_AndForAnUnknownEmail_SendsNothing_WithTheSameAnswer()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, _) = await RegisteredAsync(harness);
        var stranger = $"stranger.{Guid.NewGuid():N}@example.test";

        var known = await harness.Send(new RequestPasswordResetCommand(email.ToUpperInvariant()));
        var unknown = await harness.Send(new RequestPasswordResetCommand(stranger));

        Assert.Equal(known, unknown);
        Assert.Single(harness.Mailer.Sent, m => m.Kind == "reset" && m.To == email);
        Assert.DoesNotContain(harness.Mailer.Sent, m => m.To == stranger);
    }

    [Theory]
    [Trait("Spec", "ACT-6")]
    [InlineData("")]
    [InlineData("not an email")]
    public async Task Request_ForJunk_GivesTheSameAnswerToo(string typed)
    {
        using var harness = new AccountsHarness(postgres);

        var answer = await harness.Send(new RequestPasswordResetCommand(typed));

        Assert.Equal(PasswordResetRequested.Accepted, answer);
    }

    [Fact]
    [Trait("Spec", "ACT-7")]
    public async Task Reset_SetsTheNewPassword_TheOldOneStopsWorking_AndTheNewOneWorks()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, _) = await RegisteredAsync(harness);
        await harness.Send(new RequestPasswordResetCommand(email));

        var result = await harness.Send(new ResetPasswordCommand(LastReset(harness, email), new SecretString("Brand2NewPass")));

        Assert.Equal(ResetPasswordStatus.Reset, result.Status);
        Assert.Equal(SignInStatus.Failed, (await harness.Send(SignIn(email, "Strong1Pass"))).Status);
        harness.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(SignInStatus.SignedIn, (await harness.Send(SignIn(email, "Brand2NewPass"))).Status);
    }

    [Fact]
    [Trait("Spec", "ACT-8")]
    [Trait("Spec", "TC-ACC-07")]
    public async Task Reset_ALinkUsedTwice_OrExpired_OrUnknown_IsInvalid()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, _) = await RegisteredAsync(harness);
        await harness.Send(new RequestPasswordResetCommand(email));
        var token = LastReset(harness, email);

        Assert.Equal(ResetPasswordStatus.Reset, (await harness.Send(new ResetPasswordCommand(token, new SecretString("Brand2NewPass")))).Status);
        Assert.Equal(ResetPasswordStatus.Invalid, (await harness.Send(new ResetPasswordCommand(token, new SecretString("Other3NewPass")))).Status);
        Assert.Equal(ResetPasswordStatus.Invalid, (await harness.Send(new ResetPasswordCommand("never-issued", new SecretString("Other3NewPass")))).Status);

        await harness.Send(new RequestPasswordResetCommand(email));
        var late = LastReset(harness, email);
        harness.Clock.Advance(PasswordResetToken.Lifetime + TimeSpan.FromMinutes(1));
        Assert.Equal(ResetPasswordStatus.Invalid, (await harness.Send(new ResetPasswordCommand(late, new SecretString("Other3NewPass")))).Status);
    }

    [Fact]
    [Trait("Spec", "ACT-2")]
    public async Task Reset_AWeakPassword_NamesTheBrokenRule_AndKeepsTheLinkUsable()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, _) = await RegisteredAsync(harness);
        await harness.Send(new RequestPasswordResetCommand(email));
        var token = LastReset(harness, email);

        var weak = await harness.Send(new ResetPasswordCommand(token, new SecretString("nocapital1")));

        Assert.Equal(ResetPasswordStatus.PasswordRejected, weak.Status);
        Assert.Equal([PasswordRule.Capital], weak.UnmetRules);
        Assert.Equal(ResetPasswordStatus.Reset, (await harness.Send(new ResetPasswordCommand(token, new SecretString("Brand2NewPass")))).Status);
    }

    [Fact]
    [Trait("Spec", "ACT-13")]
    public async Task Reset_RevokesEverySessionOfTheCustomer()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, customerId) = await RegisteredAsync(harness);
        await harness.Send(SignIn(email, "Strong1Pass"));
        await harness.Send(new RequestPasswordResetCommand(email));

        await harness.Send(new ResetPasswordCommand(LastReset(harness, email), new SecretString("Brand2NewPass")));

        var open = await harness.Read(db => db.Sessions.CountAsync(s => s.CustomerId == customerId && s.RevokedAt == null));
        Assert.Equal(0, open);
    }

    [Fact]
    [Trait("Spec", "ACT-6")]
    public async Task ANewRequest_RetiresTheEarlierLink()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, _) = await RegisteredAsync(harness);
        await harness.Send(new RequestPasswordResetCommand(email));
        var first = LastReset(harness, email);
        await harness.Send(new RequestPasswordResetCommand(email));

        Assert.Equal(ResetPasswordStatus.Invalid, (await harness.Send(new ResetPasswordCommand(first, new SecretString("Brand2NewPass")))).Status);
    }

    [Fact]
    [Trait("Spec", "NFR-FT-08")]
    public async Task Request_StillAnswersNormally_WhenTheMailServerIsDown()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, _) = await RegisteredAsync(harness);
        harness.Mailer.Fail = true;

        Assert.Equal(PasswordResetRequested.Accepted, await harness.Send(new RequestPasswordResetCommand(email)));
    }

    [Fact]
    [Trait("Spec", "ACT-13")]
    public async Task ChangePassword_NeedsTheCurrentOne_AppliesTheRule_AndKeepsOnlyTheCurrentSession()
    {
        using var harness = new AccountsHarness(postgres);
        var (email, customerId) = await RegisteredAsync(harness);
        var other = (await harness.Send(SignIn(email, "Strong1Pass"))).Session!;
        harness.Clock.Advance(TimeSpan.FromMinutes(2));
        var current = (await harness.Send(SignIn(email, "Strong1Pass"))).Session!;

        Assert.Equal(ChangePasswordStatus.WrongPassword,
            await harness.Send(new ChangePasswordCommand(new SecretString("Wrong1Pass"), new SecretString("Brand2NewPass"), current.Tokens.Access.Reveal()), customerId));
        var weak = await harness.Send(new ChangePasswordCommand(new SecretString("Strong1Pass"), new SecretString("short"), current.Tokens.Access.Reveal()), customerId);
        Assert.Equal(ChangePasswordStatus.PasswordRejected, weak);

        var changed = await harness.Send(new ChangePasswordCommand(new SecretString("Strong1Pass"), new SecretString("Brand2NewPass"), current.Tokens.Access.Reveal()), customerId);

        Assert.Equal(ChangePasswordStatus.Changed, changed);
        var sessions = await harness.Read(db => db.Sessions.AsNoTracking().Where(s => s.CustomerId == customerId).ToListAsync());
        Assert.Null(sessions.Single(s => s.Id == current.SessionId).RevokedAt);
        Assert.NotNull(sessions.Single(s => s.Id == other.SessionId).RevokedAt);
    }
}
