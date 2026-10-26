using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Mfa;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Mfa;

/// <summary>MFA task 8 (AC-8): an administrator resets another person's second factor; nobody resets their own.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class ResetStaffMfaTests(IdentityPostgres postgres)
{
    private IdentityHost Host() => MfaTestHelpers.RealMfaHost(postgres, services => services.AddScoped<IUserSessionRevoker, SessionRevoker>());

    private static async Task<long> AdministratorAsync(IdentityHost host) =>
        (await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Owner))).UserId;

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task ForcesReEnrolment()
    {
        using var host = Host();
        var admin = await AdministratorAsync(host);
        var (userId, name) = await MfaTestHelpers.ReadyToEnrolAsync(host);
        var (_, codes) = await MfaTestHelpers.EnrolAsync(host, userId);
        host.Time.Now += TimeSpan.FromSeconds(31);
        var session = await host.SendAsync(new IssueSessionCommand(userId, null, "203.0.113.160", null));
        Assert.Equal(SessionScope.Full, session.Scope);

        await host.SendAsync(new ResetStaffMfaCommand(userId, admin));

        // The second factor, its recovery codes and every session are gone.
        var user = await host.LoadAsync(userId);
        Assert.False(user.IsMfaEnrolled);
        Assert.Null(user.MfaSecretCipher);
        Assert.Equal(new MfaStatus(false, 0), await host.SendAsync(new GetMfaStatusQuery(userId)));
        Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(session.Tokens.Access.Reveal())));
        await using (var scope = host.Scope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<IdntDbContext>().Sessions.AsNoTracking().SingleAsync(s => s.Id == session.SessionId);
            Assert.Equal(SessionRevokeReason.MfaReset, row.RevokeReason);
        }

        // The old recovery code no longer signs anyone in, and the next sign-in only gets a session that can enrol again.
        var signIn = await host.SendAsync(new SignInCommand(name, new SecretString(MfaTestHelpers.Password), "203.0.113.161"));
        Assert.Equal(SignInStatus.LimitedSession, signIn.Status);
        Assert.Equal(SessionScope.EnrolMfa, signIn.Scope);
        await MfaTestHelpers.EnrolAsync(host, userId);
        Assert.Equal(new MfaStatus(true, 10), await host.SendAsync(new GetMfaStatusQuery(userId)));
        Assert.NotEmpty(codes);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task NeedsPermission_AndCannotTargetSelf()
    {
        using var host = Host();
        var (userId, _) = await MfaTestHelpers.ReadyToEnrolAsync(host);
        await MfaTestHelpers.EnrolAsync(host, userId);

        // Whoever it is, a person cannot reset their own second factor this way (the permission itself is checked at the endpoint).
        var refused = await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ResetStaffMfaCommand(userId, userId)));
        Assert.Contains("your own", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True((await host.LoadAsync(userId)).IsMfaEnrolled);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task AnUnknownTarget_IsNotFound_AndAResetIsRepeatable()
    {
        using var host = Host();
        var admin = await AdministratorAsync(host);
        var (userId, _) = await MfaTestHelpers.ReadyToEnrolAsync(host);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => host.SendAsync(new ResetStaffMfaCommand(long.MaxValue - 1, admin)));

        // Not enrolled at all, or already reset: nothing to clear, and no error.
        await host.SendAsync(new ResetStaffMfaCommand(userId, admin));
        await host.SendAsync(new ResetStaffMfaCommand(userId, admin));
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task APendingSignIn_StartedBeforeTheReset_CannotFinish()
    {
        using var host = Host();
        var admin = await AdministratorAsync(host);
        var (userId, name) = await MfaTestHelpers.ReadyToEnrolAsync(host);
        var (secret, _) = await MfaTestHelpers.EnrolAsync(host, userId);
        host.Time.Now += TimeSpan.FromSeconds(31);
        var pending = (await host.SendAsync(new SignInCommand(name, new SecretString(MfaTestHelpers.Password), "203.0.113.162"))).PendingToken!;

        await host.SendAsync(new ResetStaffMfaCommand(userId, admin));
        var result = await host.SendAsync(new VerifyMfaCommand(pending.Reveal(), MfaTestHelpers.CodeNow(host, secret), "203.0.113.162", null, null));

        Assert.Equal(VerifyMfaStatus.PendingEnded, result.Status);
    }
}
