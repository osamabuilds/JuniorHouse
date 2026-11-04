using System.Security.Claims;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Sessions;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Sessions;

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class SessionAuthenticationHandlerTests(IdentityPostgres postgres)
{
    private const string Own = "Correct-Horse-Battery-Staple-42";

    /// <summary>An account that has changed its password and enrolled MFA, signed in once.</summary>
    private static async Task<(long UserId, string Name, IssuedSession Issued)> SignedInAsync(IdentityHost host, string ip)
    {
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, null, created.TemporaryPassword, new SecretString(Own)));
        host.Mfa.Enrolled.Add(created.UserId);
        var issued = await host.SendAsync(new IssueSessionCommand(created.UserId, null, ip, "agent"));
        return (created.UserId, created.UserName, issued);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task ASignedInSession_Authenticates_WithItsUserAndScope()
    {
        using var host = new IdentityHost(postgres);
        var (userId, name, issued) = await SignedInAsync(host, "203.0.113.80");

        var result = await host.SendAsync(new AuthenticateSessionQuery(issued.Tokens.Access.Reveal()));

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(issued.SessionId, result.SessionId);
        Assert.Equal(name, result.UserName);
        Assert.Equal("Test Person", result.DisplayName);
        Assert.Equal(SessionScope.Full, result.Scope);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task RejectsEveryUnusableSession()
    {
        using var host = new IdentityHost(postgres);

        // Missing, malformed, oversized and unknown values.
        foreach (var value in new[] { null, "", "   ", "not-a-token", new string('a', 500), SessionTokens.Generate().Reveal() })
        {
            Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(value)));
        }

        // Revoked: signing in again with the cookie revokes the old session.
        var (userId, _, revoked) = await SignedInAsync(host, "203.0.113.81");
        await host.SendAsync(new IssueSessionCommand(userId, revoked.Tokens.Access.Reveal(), "203.0.113.81", null));
        Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(revoked.Tokens.Access.Reveal())));

        // The access value expires after 15 minutes even though the session could still be refreshed.
        var (_, _, shortLived) = await SignedInAsync(host, "203.0.113.82");
        host.Time.Now += TimeSpan.FromMinutes(15);
        Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(shortLived.Tokens.Access.Reveal())));

        // Idle and absolute limits.
        var (_, _, idle) = await SignedInAsync(host, "203.0.113.83");
        host.Time.Now += TimeSpan.FromMinutes(31);
        Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(idle.Tokens.Access.Reveal())));

        var (_, _, longLived) = await SignedInAsync(host, "203.0.113.84");
        var token = longLived.Tokens;
        for (var i = 0; i < 40; i++)
        {
            host.Time.Now += TimeSpan.FromMinutes(20);
            var refreshed = await host.SendAsync(new RefreshSessionCommand(token.Refresh.Reveal()));
            if (refreshed.Session is null)
            {
                break;
            }

            token = refreshed.Session.Tokens;
        }

        Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(token.Access.Reveal()))); // past 12 hours

        // The old access value after a refresh does not work either.
        var (_, _, rotated) = await SignedInAsync(host, "203.0.113.85");
        host.Time.Now += TimeSpan.FromMinutes(1);
        await host.SendAsync(new RefreshSessionCommand(rotated.Tokens.Refresh.Reveal()));
        Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(rotated.Tokens.Access.Reveal())));
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task DeactivationAppliesNextRequest()
    {
        using var host = new IdentityHost(postgres);
        var (userId, _, issued) = await SignedInAsync(host, "203.0.113.86");
        Assert.NotNull(await host.SendAsync(new AuthenticateSessionQuery(issued.Tokens.Access.Reveal())));

        // The session row is left alone (the recording revoker does nothing); the request still fails because the user is inactive.
        await host.SendAsync(new DeactivateStaffCommand(userId));

        Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(issued.Tokens.Access.Reveal())));

        await host.SendAsync(new ReactivateStaffCommand(userId));
        Assert.NotNull(await host.SendAsync(new AuthenticateSessionQuery(issued.Tokens.Access.Reveal())));
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task TheScope_FollowsTheAccount_OnEveryRequest()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        var first = await host.SendAsync(new SignInCommand(created.UserName, created.TemporaryPassword, "203.0.113.87"));
        var access = first.Tokens!.Access.Reveal();

        Assert.Equal(SessionScope.ChangePassword, (await host.SendAsync(new AuthenticateSessionQuery(access)))!.Scope);

        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, first.SessionId, created.TemporaryPassword, new SecretString(Own)));
        Assert.Equal(SessionScope.EnrolMfa, (await host.SendAsync(new AuthenticateSessionQuery(access)))!.Scope);

        host.Mfa.Enrolled.Add(created.UserId);
        Assert.Equal(SessionScope.Full, (await host.SendAsync(new AuthenticateSessionQuery(access)))!.Scope);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task LoadsTheSessionAndTheUser_InOneQuery()
    {
        var counter = new CommandCounter();
        using var host = new IdentityHost(postgres, counter);
        var (_, _, issued) = await SignedInAsync(host, "203.0.113.88");
        counter.Reset();

        await host.SendAsync(new AuthenticateSessionQuery(issued.Tokens.Access.Reveal()));

        Assert.Equal(1, counter.Count);
    }
}

public sealed class SessionPrincipalTests
{
    [Fact]
    [Trait("Spec", "AC-9")]
    public void Principal_CarriesIdentityAndScope_ButNoPermissionOrRoleClaims()
    {
        var principal = SessionPrincipal.Create(new AuthenticatedSession(42, 7, "ayesha", "Ayesha K", SessionScope.EnrolMfa));

        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("7", principal.FindFirstValue("sid"));
        Assert.Equal("ayesha", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("EnrolMfa", principal.FindFirstValue(SessionPrincipal.ScopeClaim));

        // Permissions are looked up on each request (roles AC-5), so a role change applies at once and nothing goes stale in a cookie.
        Assert.DoesNotContain(principal.Claims, c => c.Type.EndsWith("role", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(principal.Claims, c => c.Type.Contains("perm", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Ayesha K", principal.FindFirstValue(SessionPrincipal.DisplayNameClaim));
        Assert.Equal(5, principal.Claims.Count());
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public void Principal_UsesTheSessionAuthenticationScheme()
    {
        var principal = SessionPrincipal.Create(new AuthenticatedSession(1, 1, "a", "A", SessionScope.Full));

        Assert.Equal(SessionPrincipal.Scheme, principal.Identity!.AuthenticationType);
    }
}
