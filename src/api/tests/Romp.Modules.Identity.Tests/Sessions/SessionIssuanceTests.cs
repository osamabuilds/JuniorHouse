using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Sessions;

public sealed class SessionScopeRuleTests
{
    [Theory]
    [Trait("Spec", "AC-9")]
    [InlineData(false, true, SessionScope.Full)]
    [InlineData(false, false, SessionScope.EnrolMfa)]
    [InlineData(true, true, SessionScope.ChangePassword)]
    [InlineData(true, false, SessionScope.ChangePassword)] // the password comes first; MFA enrolment follows
    public void For_PicksTheMostUrgentStep(bool mustChangePassword, bool mfaEnrolled, SessionScope expected)
    {
        Assert.Equal(expected, SessionScopes.For(mustChangePassword, mfaEnrolled));
    }
}

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class SessionScopeTests(IdentityPostgres postgres)
{
    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task LimitedUsers_GetLimitedScope()
    {
        using var host = new IdentityHost(postgres);
        var role = await host.RoleIdAsync(RoleCodes.Catalogue);

        // A new account signs in with its temporary password: it must change it first.
        var created = await StaffHandlerHelpers.CreateAsync(host, role);
        var first = await host.SendAsync(new SignInCommand(created.UserName, created.TemporaryPassword, "203.0.113.50"));
        Assert.Equal(SignInStatus.LimitedSession, first.Status);
        Assert.Equal(SessionScope.ChangePassword, first.Scope);
        Assert.NotNull(first.Tokens);
        Assert.Null(first.PendingToken); // no MFA step: a limited session can only fix what is missing

        // After choosing its own password it still has no MFA, so the scope is to enrol.
        const string Own = "Correct-Horse-Battery-Staple-42";
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, first.SessionId, created.TemporaryPassword, new SecretString(Own)));
        var second = await host.SendAsync(new SignInCommand(created.UserName, new SecretString(Own), "203.0.113.50"));
        Assert.Equal(SignInStatus.LimitedSession, second.Status);
        Assert.Equal(SessionScope.EnrolMfa, second.Scope);

        // Once MFA is enrolled the person gets the password step result and goes on to the MFA step.
        host.Mfa.Enrolled.Add(created.UserId);
        var third = await host.SendAsync(new SignInCommand(created.UserName, new SecretString(Own), "203.0.113.50"));
        Assert.Equal(SignInStatus.PasswordAccepted, third.Status);
        Assert.NotNull(third.PendingToken);
        Assert.Null(third.Tokens);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task TheScopeIsComputedFromTheAccount_NotStoredOnTheSession()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        host.Mfa.Enrolled.Add(created.UserId);

        var issued = await host.SendAsync(new IssueSessionCommand(created.UserId, null, "203.0.113.51", null));

        Assert.Equal(SessionScope.ChangePassword, issued.Scope); // still on the temporary password
        Assert.DoesNotContain(
            typeof(Session).GetProperties().Select(p => p.Name),
            name => name.Contains("Scope", StringComparison.Ordinal));
    }
}

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class NewSessionOnLoginTests(IdentityPostgres postgres)
{
    private const string Own = "Correct-Horse-Battery-Staple-42";

    private static async Task<(long Id, string Name)> EnrolledUserAsync(IdentityHost host)
    {
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, null, created.TemporaryPassword, new SecretString(Own)));
        host.Mfa.Enrolled.Add(created.UserId);
        return (created.UserId, created.UserName);
    }

    private static async Task<Session> LoadAsync(IdentityHost host, long sessionId)
    {
        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        return await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task PresentedSession_IsRevoked_AndNeverReused()
    {
        using var host = new IdentityHost(postgres);
        var (userId, _) = await EnrolledUserAsync(host);
        var first = await host.SendAsync(new IssueSessionCommand(userId, null, "203.0.113.60", "agent"));

        host.Time.Now += TimeSpan.FromMinutes(1);
        var second = await host.SendAsync(new IssueSessionCommand(userId, first.Tokens.Access.Reveal(), "203.0.113.60", "agent"));

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.NotEqual(first.Tokens.Access.Reveal(), second.Tokens.Access.Reveal());
        Assert.NotEqual(first.Tokens.Refresh.Reveal(), second.Tokens.Refresh.Reveal());

        var old = await LoadAsync(host, first.SessionId);
        Assert.Equal(IdentityHost.Start.AddMinutes(1), old.RevokedAt);
        Assert.Equal(SessionRevokeReason.NewSignIn, old.RevokeReason);
        Assert.False(old.AccessIsValid(host.Time.Now));

        var current = await LoadAsync(host, second.SessionId);
        Assert.Null(current.RevokedAt);
        Assert.True(current.AccessTokenMatches(second.Tokens.Access.Reveal()));
        Assert.False(current.AccessTokenMatches(first.Tokens.Access.Reveal())); // the fixated value never works on the new session
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task APresentedSession_OfAnotherUser_IsRevokedToo_AndJunkIsIgnored()
    {
        using var host = new IdentityHost(postgres);
        var (alice, _) = await EnrolledUserAsync(host);
        var (bob, _) = await EnrolledUserAsync(host);
        var aliceSession = await host.SendAsync(new IssueSessionCommand(alice, null, "203.0.113.61", null));

        // Bob signs in while sending Alice's cookie: whatever was presented is dead afterwards.
        var bobSession = await host.SendAsync(new IssueSessionCommand(bob, aliceSession.Tokens.Access.Reveal(), "203.0.113.62", null));
        var junk = await host.SendAsync(new IssueSessionCommand(bob, "not-a-real-token", "203.0.113.62", null));

        Assert.NotNull((await LoadAsync(host, aliceSession.SessionId)).RevokedAt);
        Assert.Null((await LoadAsync(host, bobSession.SessionId)).RevokedAt);
        Assert.Null((await LoadAsync(host, junk.SessionId)).RevokedAt);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task SigningIn_WithALimitedAccount_RevokesThePresentedSession()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        var first = await host.SendAsync(new SignInCommand(created.UserName, created.TemporaryPassword, "203.0.113.63"));
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, first.SessionId, created.TemporaryPassword, new SecretString(Own)));

        var again = await host.SendAsync(
            new SignInCommand(created.UserName, new SecretString(Own), "203.0.113.63", first.Tokens!.Access.Reveal(), "agent"));

        Assert.NotEqual(first.SessionId, again.SessionId);
        Assert.NotNull((await LoadAsync(host, first.SessionId!.Value)).RevokedAt);
        Assert.Null((await LoadAsync(host, again.SessionId!.Value)).RevokedAt);
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task TheIssuedSession_StoresHashesAndTheClientDetails_WithTheConfiguredLifetimes()
    {
        using var host = new IdentityHost(postgres);
        var (userId, _) = await EnrolledUserAsync(host);

        var issued = await host.SendAsync(new IssueSessionCommand(userId, null, "203.0.113.64", "Mozilla/5.0 test"));

        var stored = await LoadAsync(host, issued.SessionId);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal("203.0.113.64", stored.ClientIp);
        Assert.Equal("Mozilla/5.0 test", stored.UserAgent);
        Assert.Equal(SessionTokens.Hash(issued.Tokens.Refresh.Reveal()), stored.RefreshTokenHash);
        Assert.Equal(IdentityHost.Start.AddMinutes(15), issued.AccessExpiresAt);
        Assert.Equal(IdentityHost.Start.AddMinutes(30), issued.RefreshExpiresAt);
        Assert.Equal(SessionScope.Full, issued.Scope);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task AnInactiveAccount_GetsNoSession()
    {
        using var host = new IdentityHost(postgres);
        var (userId, _) = await EnrolledUserAsync(host);
        await host.SendAsync(new DeactivateStaffCommand(userId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.SendAsync(new IssueSessionCommand(userId, null, "203.0.113.65", null)));
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task TheEleventhSession_RevokesTheOldest()
    {
        using var host = new IdentityHost(postgres);
        var (userId, _) = await EnrolledUserAsync(host);
        var ids = new List<long>();
        for (var i = 0; i < Session.MaxPerUser + 1; i++)
        {
            host.Time.Now += TimeSpan.FromSeconds(1);
            ids.Add((await host.SendAsync(new IssueSessionCommand(userId, null, "203.0.113.66", null))).SessionId);
        }

        var oldest = await LoadAsync(host, ids[0]);
        Assert.Equal(SessionRevokeReason.SessionLimit, oldest.RevokeReason);
        foreach (var id in ids.Skip(1))
        {
            Assert.Null((await LoadAsync(host, id)).RevokedAt);
        }
    }
}
