using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Identity.Application.Mfa;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Domain.Mfa;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Mfa;

/// <summary>MFA task 7 (AC-3, AC-4, AC-5, AC-6): the second step of sign-in.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class MfaVerifyTests(IdentityPostgres postgres)
{
    private const string Ip = "203.0.113.120";

    /// <summary>An enrolled account whose confirming code has been spent; time is moved on so the next code is a fresh step.</summary>
    private static async Task<(long UserId, string Name, byte[] Secret, IReadOnlyList<string> Codes)> EnrolledAsync(IdentityHost host)
    {
        var (userId, name) = await MfaTestHelpers.ReadyToEnrolAsync(host);
        var (secret, codes) = await MfaTestHelpers.EnrolAsync(host, userId);
        host.Time.Now += TimeSpan.FromSeconds(31);
        return (userId, name, secret, codes);
    }

    private static async Task<SecretString> PendingAsync(IdentityHost host, string name, string ip = Ip)
    {
        var result = await host.SendAsync(new SignInCommand(name, new SecretString(MfaTestHelpers.Password), ip));
        Assert.Equal(SignInStatus.PasswordAccepted, result.Status);
        return result.PendingToken!;
    }

    private static Task<VerifyMfaResult> VerifyAsync(IdentityHost host, SecretString pending, string code, string ip = Ip, string? presented = null) =>
        host.SendAsync(new VerifyMfaCommand(pending.Reveal(), code, ip, presented, "agent"));

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task AValidCode_IssuesAFullSession_AndTheStepIsRecorded()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, name, secret, _) = await EnrolledAsync(host);
        var pending = await PendingAsync(host, name);

        var result = await VerifyAsync(host, pending, MfaTestHelpers.CodeNow(host, secret));

        Assert.Equal(VerifyMfaStatus.Succeeded, result.Status);
        Assert.Equal(SessionScope.Full, result.Session!.Scope);
        Assert.Equal(Totp.StepOf(host.Time.Now), (await host.LoadAsync(userId)).MfaLastStep);
        Assert.NotNull(await host.SendAsync(new AuthenticateSessionQuery(result.Session.Tokens.Access.Reveal())));

        // The pending sign-in is spent: the same token cannot be used again.
        var again = await VerifyAsync(host, pending, MfaTestHelpers.CodeNow(host, secret, 1));
        Assert.Equal(VerifyMfaStatus.PendingEnded, again.Status);
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task OneStepOfDrift_IsAccepted()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (_, name, secret, _) = await EnrolledAsync(host);
        var pending = await PendingAsync(host, name);

        var result = await VerifyAsync(host, pending, MfaTestHelpers.CodeNow(host, secret, stepOffset: 1)); // a phone clock 30 s fast

        Assert.Equal(VerifyMfaStatus.Succeeded, result.Status);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task ACodeThatWasAlreadyAccepted_IsRejected()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (_, name, secret, _) = await EnrolledAsync(host);
        var code = MfaTestHelpers.CodeNow(host, secret);
        Assert.Equal(VerifyMfaStatus.Succeeded, (await VerifyAsync(host, await PendingAsync(host, name), code)).Status);

        // Someone who saw the code over a shoulder, within its validity window.
        var replay = await VerifyAsync(host, await PendingAsync(host, name, "203.0.113.121"), code, "203.0.113.121");

        Assert.Equal(VerifyMfaStatus.Failed, replay.Status);
        Assert.Null(replay.Session);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task TheCodeThatConfirmedEnrolment_CannotBeUsedToSignIn()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, name) = await MfaTestHelpers.ReadyToEnrolAsync(host);
        var (secret, _) = await MfaTestHelpers.EnrolAsync(host, userId); // time has not moved: the confirming code is still "current"

        var result = await VerifyAsync(host, await PendingAsync(host, name), MfaTestHelpers.CodeNow(host, secret));

        Assert.Equal(VerifyMfaStatus.Failed, result.Status);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task ARecoveryCode_WorksOnce()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, name, _, codes) = await EnrolledAsync(host);

        var first = await VerifyAsync(host, await PendingAsync(host, name), codes[2]);
        Assert.Equal(VerifyMfaStatus.Succeeded, first.Status);
        Assert.True(first.UsedRecoveryCode);
        Assert.Equal(new MfaStatus(true, 9), await host.SendAsync(new GetMfaStatusQuery(userId)));

        var second = await VerifyAsync(host, await PendingAsync(host, name, "203.0.113.122"), codes[2], "203.0.113.122");
        Assert.Equal(VerifyMfaStatus.Failed, second.Status);

        // Another code still works, typed in lower case without the dash.
        var other = await VerifyAsync(host, await PendingAsync(host, name, "203.0.113.123"), codes[5].Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant(), "203.0.113.123");
        Assert.Equal(VerifyMfaStatus.Succeeded, other.Status);
        Assert.Equal(8, (await host.SendAsync(new GetMfaStatusQuery(userId))).RecoveryCodesRemaining);
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task FiveFailures_EndThePendingSignIn()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, name, secret, _) = await EnrolledAsync(host);
        var pending = await PendingAsync(host, name);
        var statuses = new List<VerifyMfaStatus>();
        for (var i = 0; i < 5; i++)
        {
            // Five steps ahead of the clock is never accepted (worked out each time, because the clock moves on).
            statuses.Add((await VerifyAsync(host, pending, MfaTestHelpers.CodeNow(host, secret, 5))).Status);
            host.Time.Now += TimeSpan.FromSeconds(61); // wait out the delay a run of failures starts
        }

        Assert.Equal([.. Enumerable.Repeat(VerifyMfaStatus.Failed, 4), VerifyMfaStatus.PendingEnded], statuses);

        // Even the right code no longer works with that pending sign-in.
        var late = await VerifyAsync(host, pending, MfaTestHelpers.CodeNow(host, secret));
        Assert.Equal(VerifyMfaStatus.PendingEnded, late.Status);

        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        Assert.Empty(await db.PendingSignIns.AsNoTracking().Where(p => p.UserId == userId).ToListAsync());
        Assert.Equal(5, await db.LoginAttempts.AsNoTracking().CountAsync(a => a.UserId == userId && a.Outcome == LoginOutcome.MfaBad));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task TheFailures_FeedTheThrottleOfTheSessionsFeature()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (_, name, secret, _) = await EnrolledAsync(host);
        var pending = await PendingAsync(host, name);
        var wrong = MfaTestHelpers.CodeNow(host, secret, 5);

        for (var i = 0; i < 3; i++)
        {
            await VerifyAsync(host, pending, wrong); // three wrong codes in a row start the delay
        }

        // The password step now waits, even with the right password, and so does another code attempt; neither counts as a failure.
        var signIn = await host.SendAsync(new SignInCommand(name, new SecretString(MfaTestHelpers.Password), Ip));
        Assert.Equal(SignInStatus.Throttled, signIn.Status);
        var blocked = await VerifyAsync(host, pending, MfaTestHelpers.CodeNow(host, secret));
        Assert.Equal(VerifyMfaStatus.Throttled, blocked.Status);
        Assert.True(blocked.RetryAfterSeconds > 0);

        // Once the delay has passed the right code works: three failures did not destroy the pending sign-in.
        host.Time.Now += TimeSpan.FromSeconds(31);
        Assert.Equal(VerifyMfaStatus.Succeeded, (await VerifyAsync(host, pending, MfaTestHelpers.CodeNow(host, secret))).Status);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task ParallelSameCode_OnlyOneSucceeds()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (_, name, secret, _) = await EnrolledAsync(host);
        var pendings = new List<(SecretString Token, string Ip)>();
        for (var i = 0; i < 4; i++)
        {
            var ip = $"203.0.113.{130 + i}";
            pendings.Add((await PendingAsync(host, name, ip), ip));
        }

        var code = MfaTestHelpers.CodeNow(host, secret);
        var results = await Task.WhenAll(pendings.Select(p => VerifyAsync(host, p.Token, code, p.Ip)));

        Assert.Equal(1, results.Count(r => r.Status == VerifyMfaStatus.Succeeded));
        Assert.Equal(3, results.Count(r => r.Status == VerifyMfaStatus.Failed));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task ParallelSameRecoveryCode_OnlyOneSucceeds()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (_, name, _, codes) = await EnrolledAsync(host);
        var pendings = new List<(SecretString Token, string Ip)>();
        for (var i = 0; i < 4; i++)
        {
            var ip = $"203.0.113.{140 + i}";
            pendings.Add((await PendingAsync(host, name, ip), ip));
        }

        var results = await Task.WhenAll(pendings.Select(p => VerifyAsync(host, p.Token, codes[0], p.Ip)));

        Assert.Equal(1, results.Count(r => r.Status == VerifyMfaStatus.Succeeded));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task UnknownExpiredAndInactive_AreAllJustAnEndedSignIn()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, name, secret, _) = await EnrolledAsync(host);

        Assert.Equal(VerifyMfaStatus.PendingEnded, (await VerifyAsync(host, SessionTokens.Generate(), MfaTestHelpers.CodeNow(host, secret))).Status);

        var expiring = await PendingAsync(host, name);
        host.Time.Now += PendingSignIn.Lifetime + TimeSpan.FromSeconds(1);
        Assert.Equal(VerifyMfaStatus.PendingEnded, (await VerifyAsync(host, expiring, MfaTestHelpers.CodeNow(host, secret))).Status);

        var live = await PendingAsync(host, name, "203.0.113.150");
        await host.SendAsync(new Romp.Modules.Identity.Application.StaffAccounts.DeactivateStaffCommand(userId));
        Assert.Equal(VerifyMfaStatus.PendingEnded, (await VerifyAsync(host, live, MfaTestHelpers.CodeNow(host, secret), "203.0.113.150")).Status);
    }

    [Theory]
    [Trait("Spec", "AC-3")]
    [InlineData("12345")]
    [InlineData("not a code")]
    [InlineData("ABCDE-FGHJK-LMNPQ")]
    public async Task ACodeThatIsNeitherShape_IsAnOrdinaryFailure(string typed)
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (_, name, _, _) = await EnrolledAsync(host);

        var result = await VerifyAsync(host, await PendingAsync(host, name), typed);

        Assert.Equal(VerifyMfaStatus.Failed, result.Status);
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task AbsurdInput_IsRejectedBeforeAnyWork()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new VerifyMfaCommand(string.Empty, "123456", Ip, null, null)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new VerifyMfaCommand(new string('a', 200), "123456", Ip, null, null)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new VerifyMfaCommand("token", new string('1', 100), Ip, null, null)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new VerifyMfaCommand("token", "123456", string.Empty, null, null)));
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task ThePresentedSessionCookie_IsRevoked_WhenTheSecondStepSucceeds()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, name, secret, _) = await EnrolledAsync(host);
        var stale = await host.SendAsync(new IssueSessionCommand(userId, null, Ip, null));

        var result = await VerifyAsync(host, await PendingAsync(host, name), MfaTestHelpers.CodeNow(host, secret), presented: stale.Tokens.Access.Reveal());

        Assert.Equal(VerifyMfaStatus.Succeeded, result.Status);
        Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(stale.Tokens.Access.Reveal())));
        Assert.NotEqual(stale.SessionId, result.Session!.SessionId);
    }
}
