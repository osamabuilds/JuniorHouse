using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Sessions;

/// <summary>Sessions task 12 (AC-11, staff-accounts AC-7/AC-8): ending sessions, one, all but one, or every one of an account.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class SessionRevocationTests(IdentityPostgres postgres)
{
    /// <summary>A host that uses the real revoker instead of the recording fake.</summary>
    private IdentityHost RealHost() => new(postgres, configure: services => services.AddScoped<IUserSessionRevoker, SessionRevoker>());

    private static async Task<long> NewUserAsync(IdentityHost host) =>
        (await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue))).UserId;

    private static async Task<Session> LoadAsync(IdentityHost host, long sessionId)
    {
        await using var scope = host.Scope();
        return await scope.ServiceProvider.GetRequiredService<IdntDbContext>().Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
    }

    private static Task<IssuedSession> SignInAsync(IdentityHost host, long userId) =>
        host.SendAsync(new IssueSessionCommand(userId, null, "203.0.113.90", "agent"));

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task SignOut_EndsOnlyThatSession_AndIsIdempotent()
    {
        using var host = RealHost();
        var userId = await NewUserAsync(host);
        var first = await SignInAsync(host, userId);
        var second = await SignInAsync(host, userId);

        await host.SendAsync(new SignOutCommand(first.Tokens.Access.Reveal()));
        host.Time.Now += TimeSpan.FromMinutes(1);
        await host.SendAsync(new SignOutCommand(first.Tokens.Access.Reveal())); // again: nothing to do, no error

        var ended = await LoadAsync(host, first.SessionId);
        Assert.Equal(SessionRevokeReason.SignOut, ended.RevokedAt is null ? null : ended.RevokeReason);
        Assert.Equal(IdentityHost.Start, ended.RevokedAt); // the first sign-out's date stands
        Assert.Null((await LoadAsync(host, second.SessionId)).RevokedAt);
    }

    [Theory]
    [Trait("Spec", "AC-11")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task SignOut_WithNoUsableCookie_IsAQuietNoOp(string? cookie)
    {
        using var host = RealHost();

        await host.SendAsync(new SignOutCommand(cookie));
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task SignOutEverywhere_EndsEveryActiveSessionOfTheUser_AndNoOneElses()
    {
        using var host = RealHost();
        var userId = await NewUserAsync(host);
        var otherUser = await NewUserAsync(host);
        var mine = new[] { await SignInAsync(host, userId), await SignInAsync(host, userId), await SignInAsync(host, userId) };
        var theirs = await SignInAsync(host, otherUser);

        var ended = await host.SendAsync(new SignOutEverywhereCommand(userId));

        Assert.Equal(3, ended);
        foreach (var session in mine)
        {
            var row = await LoadAsync(host, session.SessionId);
            Assert.Equal(SessionRevokeReason.SignOutEverywhere, row.RevokeReason);
            Assert.False(row.AccessIsValid(host.Time.Now));
        }

        Assert.Null((await LoadAsync(host, theirs.SessionId)).RevokedAt);
    }

    [Fact]
    [Trait("Spec", "AC-11")]
    public async Task TheRevoker_KeepsTheNamedSession_AndTheFirstReasonOfAnEndedOne()
    {
        using var host = RealHost();
        var userId = await NewUserAsync(host);
        var keep = await SignInAsync(host, userId);
        var other = await SignInAsync(host, userId);
        var alreadyEnded = await SignInAsync(host, userId);
        await host.SendAsync(new SignOutCommand(alreadyEnded.Tokens.Access.Reveal()));

        await using (var scope = host.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<IUserSessionRevoker>()
                .RevokeAsync(userId, keep.SessionId, SessionRevokeReason.PasswordChanged, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IdntDbContext>().SaveChangesAsync();
        }

        Assert.Null((await LoadAsync(host, keep.SessionId)).RevokedAt);
        Assert.Equal(SessionRevokeReason.PasswordChanged, (await LoadAsync(host, other.SessionId)).RevokeReason);
        Assert.Equal(SessionRevokeReason.SignOut, (await LoadAsync(host, alreadyEnded.SessionId)).RevokeReason);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task Deactivating_AnAccount_EndsItsSessionsAtOnce()
    {
        using var host = RealHost();
        var userId = await NewUserAsync(host);
        var session = await SignInAsync(host, userId);

        await host.SendAsync(new DeactivateStaffCommand(userId));

        var row = await LoadAsync(host, session.SessionId);
        Assert.Equal(SessionRevokeReason.AccountDeactivated, row.RevokeReason);
        Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(session.Tokens.Access.Reveal())));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task ChangingYourPassword_EndsTheOtherSessions_ButNotThisOne()
    {
        using var host = RealHost();
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        var thisOne = await SignInAsync(host, created.UserId);
        var elsewhere = await SignInAsync(host, created.UserId);

        await host.SendAsync(new ChangeOwnPasswordCommand(
            created.UserId, thisOne.SessionId, created.TemporaryPassword, new Romp.Modules.Identity.Domain.StaffAccounts.SecretString("Correct-Horse-Battery-Staple-42")));

        Assert.Null((await LoadAsync(host, thisOne.SessionId)).RevokedAt);
        Assert.Equal(SessionRevokeReason.PasswordChanged, (await LoadAsync(host, elsewhere.SessionId)).RevokeReason);
    }
}
