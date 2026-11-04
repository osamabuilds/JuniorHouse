using Romp.BuildingBlocks.Web.Captcha;
using Microsoft.EntityFrameworkCore;
using Romp.Modules.Customers.Application.Accounts;
using Romp.Modules.Customers.Domain.Accounts;
using Romp.Modules.Customers.Infrastructure.Accounts;
using Romp.Modules.Customers.Tests.Support;

namespace Romp.Modules.Customers.Tests.Accounts;

/// <summary>customers/accounts S5 (ACT-4, ACT-5, ACT-9 to ACT-12): password sign-in, the throttle, the attempt log and sessions.</summary>
[Collection(CustomersPostgresFixtureDefinition.Name)]
public sealed class SignInTests(CustomersPostgres postgres)
{
    private const string Password = "Strong1Pass";

    // xUnit makes a new instance per test, so every test gets its own client address: the throttle counts failures per address and the database is shared.
    private readonly string _ip = $"198.51.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

    private SignInCommand SignIn(string email, string password = Password, string? ip = null, string? captcha = null, bool keep = false, string? presented = null) =>
        new(email, new SecretString(password), keep, captcha, ip ?? _ip, presented, "TestBrowser/1.0");

    private static async Task<string> RegisteredAsync(AccountsHarness harness)
    {
        var email = $"signin.{Guid.NewGuid():N}@example.test";
        await harness.Send(RegisterTests.Register(email, Password));
        return email;
    }

    [Fact]
    [Trait("Spec", "ACT-4")]
    [Trait("Spec", "TC-ACC-04")]
    [Trait("Spec", "ACT-10")]
    public async Task SignIn_ValidCredentials_IssuesASession_AndLogsOneOkAttempt()
    {
        using var harness = new AccountsHarness(postgres);
        var email = await RegisteredAsync(harness);

        var result = await harness.Send(SignIn(email.ToUpperInvariant()));

        Assert.Equal(SignInStatus.SignedIn, result.Status);
        Assert.NotNull(result.Session);
        var attempts = await harness.Read(db => db.LoginAttempts.AsNoTracking().Where(a => a.AttemptedEmail == email).ToListAsync());
        var ok = Assert.Single(attempts, a => a.Outcome == LoginOutcome.Ok);
        Assert.Equal(_ip, ok.ClientIp);
        Assert.Equal(result.Session!.CustomerId, ok.CustomerId);
        var customer = await harness.Read(db => db.Customers.AsNoTracking().SingleAsync(c => c.Id == result.Session.CustomerId));
        Assert.NotNull(customer.LastSignedInAt);
    }

    [Fact]
    [Trait("Spec", "ACT-5")]
    [Trait("Spec", "NFR-15")]
    public async Task SignIn_WrongPasswordAndUnknownEmail_GiveTheSameAnswer_AndNeverRevealWhichWasWrong()
    {
        using var harness = new AccountsHarness(postgres);
        var email = await RegisteredAsync(harness);

        var wrongPassword = await harness.Send(SignIn(email, "Wrong1Pass"));
        var unknownEmail = await harness.Send(SignIn($"nobody.{Guid.NewGuid():N}@example.test", Password, ip: "192.0.2." + Random.Shared.Next(1, 255)));

        Assert.Equal(SignInStatus.Failed, wrongPassword.Status);
        Assert.Equal(wrongPassword, unknownEmail);
        Assert.Null(wrongPassword.Session);
    }

    [Fact]
    [Trait("Spec", "ACT-5")]
    public async Task SignIn_AnAccountWithoutAPassword_FailsLikeAnyOtherBadSignIn()
    {
        using var harness = new AccountsHarness(postgres);
        var email = $"nopass.{Guid.NewGuid():N}@example.test";
        await harness.Seed(db => db.Customers.Add(Customer.Create(email, "No", "Pass")));

        var result = await harness.Send(SignIn(email));

        Assert.Equal(SignInStatus.Failed, result.Status);
    }

    [Fact]
    [Trait("Spec", "ACT-10")]
    [Trait("Spec", "SEC-25")]
    public async Task SignIn_EveryAttemptIsOneRow_WithTheTypedEmailCleaned_AndNoPassword()
    {
        using var harness = new AccountsHarness(postgres);
        var guid = Guid.NewGuid().ToString("N");
        var typed = $"  Weird\u0007.{guid}@Example.Test  ";
        var cleaned = $"weird.{guid}@example.test";

        await harness.Send(SignIn(typed, "Wrong1Pass"));

        var rows = await harness.Read(db => db.LoginAttempts.AsNoTracking().Where(a => a.AttemptedEmail == cleaned).ToListAsync());
        var row = Assert.Single(rows);
        Assert.Equal(LoginOutcome.BadCredentials, row.Outcome);
        Assert.Null(row.CustomerId);
        Assert.DoesNotContain(typeof(LoginAttempt).GetProperties(), p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Spec", "ACT-9")]
    [Trait("Spec", "TC-ACC-05")]
    [Trait("Spec", "SEC-02")]
    public async Task SignIn_FromTheThirdFailure_IsDelayedAndNeedsTheCaptcha_ButTheAccountIsNeverLocked()
    {
        using var harness = new AccountsHarness(postgres);
        var email = await RegisteredAsync(harness);

        for (var i = 0; i < SignInThrottle.FreeFailures; i++)
        {
            Assert.Equal(SignInStatus.Failed, (await harness.Send(SignIn(email, "Wrong1Pass"))).Status);
        }

        // The 3rd failure is still answered as a plain failure; it is the attempts after it that are delayed.
        Assert.Equal(SignInStatus.Failed, (await harness.Send(SignIn(email, "Wrong1Pass"))).Status);

        var delayed = await harness.Send(SignIn(email, Password, captcha: FakeCaptchaVerifier.PassToken));
        Assert.Equal(SignInStatus.Throttled, delayed.Status);
        Assert.InRange(delayed.RetryAfterSeconds ?? 0, 1, (int)SignInThrottle.MaxDelay.TotalSeconds);

        harness.Clock.Advance(TimeSpan.FromSeconds(61));

        Assert.Equal(SignInStatus.CaptchaRequired, (await harness.Send(SignIn(email, Password))).Status);
        harness.Clock.Advance(TimeSpan.FromSeconds(61)); // the missing captcha counted as a failure, so it lengthened the delay
        var ok =await harness.Send(SignIn(email, Password, captcha: FakeCaptchaVerifier.PassToken));
        Assert.Equal(SignInStatus.SignedIn, ok.Status);
    }

    [Fact]
    [Trait("Spec", "ACT-9")]
    public async Task SignIn_ThrottledAttempts_DoNotLengthenTheDelay()
    {
        using var harness = new AccountsHarness(postgres);
        var email = await RegisteredAsync(harness);
        for (var i = 0; i < 3; i++)
        {
            await harness.Send(SignIn(email, "Wrong1Pass"));
        }

        var first = await harness.Send(SignIn(email, Password, captcha: FakeCaptchaVerifier.PassToken));
        for (var i = 0; i < 5; i++)
        {
            await harness.Send(SignIn(email, Password, captcha: FakeCaptchaVerifier.PassToken));
        }

        var later = await harness.Send(SignIn(email, Password, captcha: FakeCaptchaVerifier.PassToken));
        Assert.Equal(SignInStatus.Throttled, first.Status);
        Assert.Equal(SignInStatus.Throttled, later.Status);
        Assert.True(later.RetryAfterSeconds <= first.RetryAfterSeconds);
    }

    [Fact]
    [Trait("Spec", "ACT-9")]
    public async Task SignIn_ABadCaptcha_IsRefused_AndLoggedAsACaptchaFailure()
    {
        using var harness = new AccountsHarness(postgres);
        var email = await RegisteredAsync(harness);
        for (var i = 0; i < 3; i++)
        {
            await harness.Send(SignIn(email, "Wrong1Pass"));
        }

        harness.Clock.Advance(TimeSpan.FromMinutes(2));
        var result = await harness.Send(SignIn(email, Password, captcha: "forged"));

        Assert.Equal(SignInStatus.CaptchaRequired, result.Status);
        var rows = await harness.Read(db => db.LoginAttempts.AsNoTracking().Where(a => a.AttemptedEmail == email).ToListAsync());
        Assert.Contains(rows, a => a.Outcome == LoginOutcome.CaptchaFailed);
    }

    [Fact]
    [Trait("Spec", "ACT-11")]
    [Trait("Spec", "SEC-04")]
    public async Task SignIn_RevokesTheSessionCookieThatCameWithTheRequest()
    {
        using var harness = new AccountsHarness(postgres);
        var email = await RegisteredAsync(harness);
        var first = (await harness.Send(SignIn(email))).Session!;

        var second = (await harness.Send(SignIn(email, presented: first.Tokens.Access.Reveal()))).Session!;

        Assert.NotEqual(first.Tokens.Access.Reveal(), second.Tokens.Access.Reveal());
        var old = await harness.Read(db => db.Sessions.AsNoTracking().SingleAsync(s => s.Id == first.SessionId));
        Assert.Equal(CustomerSessionRevokeReason.NewSignIn, old.RevokeReason);
    }

    [Fact]
    [Trait("Spec", "ACT-11")]
    public async Task SignIn_StoresOnlyHashes_NeverTheCookieValues()
    {
        using var harness = new AccountsHarness(postgres);
        var email = await RegisteredAsync(harness);

        var session = (await harness.Send(SignIn(email))).Session!;

        var stored = await harness.Read(db => db.Sessions.AsNoTracking().SingleAsync(s => s.Id == session.SessionId));
        Assert.True(stored.AccessTokenMatches(session.Tokens.Access.Reveal()));
        Assert.DoesNotContain(session.Tokens.Access.Reveal(), Convert.ToBase64String(stored.AccessTokenHash), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public async Task SignIn_KeepMeSignedIn_UsesTheLongLifetimes()
    {
        using var harness = new AccountsHarness(postgres);
        var email = await RegisteredAsync(harness);

        var session = (await harness.Send(SignIn(email, keep: true))).Session!;

        Assert.True(session.RefreshExpiresAt - harness.Clock.GetUtcNow() >= TimeSpan.FromDays(29));
        Assert.True(session.AccessExpiresAt - harness.Clock.GetUtcNow() <= TimeSpan.FromMinutes(15));
    }

    [Fact]
    [Trait("Spec", "ACT-12")]
    public async Task SignIn_ManySessions_KeepsOnlyTheNewestTen()
    {
        using var harness = new AccountsHarness(postgres);
        var email = await RegisteredAsync(harness);
        long customerId = 0;
        for (var i = 0; i < CustomerSession.MaxPerCustomer + 1; i++)
        {
            harness.Clock.Advance(TimeSpan.FromSeconds(1));
            customerId = (await harness.Send(SignIn(email, ip: $"192.0.2.{i + 10}"))).Session!.CustomerId;
        }

        var open = await harness.Read(db => db.Sessions.CountAsync(s => s.CustomerId == customerId && s.RevokedAt == null));
        Assert.Equal(CustomerSession.MaxPerCustomer, open);
    }
}
