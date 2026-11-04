using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.BuildingBlocks.Domain;
using Romp.Modules.Identity.Application.Mfa;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Mfa;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Mfa;

internal static class MfaTestHelpers
{
    private const string OwnPassword = "Correct-Horse-Battery-Staple-42";

    /// <summary>A host that uses the real MFA status and reset (the default test host fakes them).</summary>
    public static IdentityHost RealMfaHost(IdentityPostgres postgres, Action<IServiceCollection>? more = null) => new(postgres, configure: services =>
    {
        services.AddScoped<IMfaEnrolmentStatus, MfaEnrolmentStatus>();
        services.AddScoped<IMfaEnrolmentReset, MfaEnrolmentReset>();
        more?.Invoke(services);
    });

    /// <summary>An account that has chosen its own password and so owes only MFA enrolment.</summary>
    public static async Task<(long UserId, string Name)> ReadyToEnrolAsync(IdentityHost host)
    {
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, null, created.TemporaryPassword, new SecretString(OwnPassword)));
        return (created.UserId, created.UserName);
    }

    public static string Password => OwnPassword;

    /// <summary>The authenticator secret as a person's app would hold it, from the text the enrolment screen shows.</summary>
    public static byte[] SecretOf(MfaEnrolmentStarted started) => Base32.Decode(started.SecretText.Reveal());

    public static string CodeNow(IdentityHost host, byte[] secret, int stepOffset = 0) =>
        Totp.Code(secret, Totp.StepOf(host.Time.Now) + stepOffset);

    /// <summary>Starts and confirms enrolment; returns the secret and the recovery codes.</summary>
    public static async Task<(byte[] Secret, IReadOnlyList<string> Codes)> EnrolAsync(IdentityHost host, long userId)
    {
        var started = await host.SendAsync(new StartMfaEnrolmentCommand(userId));
        var secret = SecretOf(started);
        var issued = await host.SendAsync(new ConfirmMfaEnrolmentCommand(userId, CodeNow(host, secret)));
        return (secret, issued.Codes.Select(c => c.Reveal()).ToList());
    }
}

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class MfaEnrolmentTests(IdentityPostgres postgres)
{
    private static async Task<StaffUser> LoadAsync(IdentityHost host, long userId) => await host.LoadAsync(userId);

    private static async Task<List<MfaEnrolmentPending>> PendingOfAsync(IdentityHost host, long userId)
    {
        await using var scope = host.Scope();
        return await scope.ServiceProvider.GetRequiredService<IdntDbContext>().MfaEnrolmentsPending.AsNoTracking().Where(p => p.UserId == userId).ToListAsync();
    }

    private static async Task<List<RecoveryCode>> CodesOfAsync(IdentityHost host, long userId)
    {
        await using var scope = host.Scope();
        return await scope.ServiceProvider.GetRequiredService<IdntDbContext>().RecoveryCodes.AsNoTracking().Where(c => c.UserId == userId).ToListAsync();
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task SecretIsInactiveUntilConfirmed()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, name) = await MfaTestHelpers.ReadyToEnrolAsync(host);

        var started = await host.SendAsync(new StartMfaEnrolmentCommand(userId));

        // 160 bits, shown as text and as the address the QR code carries, which names the account and the issuer.
        var secret = MfaTestHelpers.SecretOf(started);
        Assert.Equal(20, secret.Length);
        Assert.Contains($"secret={Base32.Encode(secret)}", started.OtpAuthUri.Reveal(), StringComparison.Ordinal);
        Assert.Contains(name, started.OtpAuthUri.Reveal(), StringComparison.Ordinal);
        Assert.Equal(host.Time.Now + MfaEnrolmentPending.Lifetime, started.ExpiresAt);

        // Nothing is active: the account has no secret, and the pending row holds only ciphertext.
        var user = await LoadAsync(host, userId);
        Assert.False(user.IsMfaEnrolled);
        Assert.Null(user.MfaSecretCipher);
        var pending = Assert.Single(await PendingOfAsync(host, userId));
        Assert.False(pending.SecretCipher.AsSpan().IndexOf(secret) >= 0);

        // The password step still only gets a session that can enrol.
        var signIn = await host.SendAsync(new SignInCommand(name, new SecretString(MfaTestHelpers.Password), "203.0.113.110"));
        Assert.Equal(SignInStatus.LimitedSession, signIn.Status);
        Assert.Equal(SessionScope.EnrolMfa, signIn.Scope);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task StartingAgain_ReplacesTheUnconfirmedSecret()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, _) = await MfaTestHelpers.ReadyToEnrolAsync(host);

        var first = await host.SendAsync(new StartMfaEnrolmentCommand(userId));
        var second = await host.SendAsync(new StartMfaEnrolmentCommand(userId));

        Assert.NotEqual(first.SecretText.Reveal(), second.SecretText.Reveal());
        Assert.Single(await PendingOfAsync(host, userId));
        // The first secret no longer confirms; the second does.
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ConfirmMfaEnrolmentCommand(userId, MfaTestHelpers.CodeNow(host, MfaTestHelpers.SecretOf(first)))));
        await host.SendAsync(new ConfirmMfaEnrolmentCommand(userId, MfaTestHelpers.CodeNow(host, MfaTestHelpers.SecretOf(second))));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task RecoveryCodes_AreReturnedOnce()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, _) = await MfaTestHelpers.ReadyToEnrolAsync(host);

        var (secret, codes) = await MfaTestHelpers.EnrolAsync(host, userId);

        Assert.Equal(10, codes.Count);
        var user = await LoadAsync(host, userId);
        Assert.True(user.IsMfaEnrolled);
        Assert.Equal(Totp.StepOf(host.Time.Now), user.MfaLastStep); // the confirming code cannot be replayed to sign in
        Assert.Empty(await PendingOfAsync(host, userId));

        // The database holds only hashes and a sealed secret: nothing shown to the person is stored.
        var rows = await CodesOfAsync(host, userId);
        Assert.Equal(10, rows.Count);
        var storedText = string.Join(' ', rows.Select(r => Convert.ToHexString(r.CodeHash))) + Convert.ToHexString(user.MfaSecretCipher!);
        Assert.All(codes, c => Assert.DoesNotContain(c.Replace("-", string.Empty, StringComparison.Ordinal), storedText, StringComparison.OrdinalIgnoreCase));
        Assert.False(user.MfaSecretCipher!.AsSpan().IndexOf(secret) >= 0);

        // It cannot be done twice: there is nothing pending, and the account is enrolled.
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ConfirmMfaEnrolmentCommand(userId, MfaTestHelpers.CodeNow(host, secret, 1))));
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new StartMfaEnrolmentCommand(userId)));

        // The status shows how many codes remain and never the secret.
        var status = await host.SendAsync(new GetMfaStatusQuery(userId));
        Assert.Equal(new MfaStatus(true, 10), status);
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task UnenrolledUser_CanReachOnlyEnrolment()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));

        // A new account first has to choose its own password; enrolment waits for that.
        var first = await host.SendAsync(new SignInCommand(created.UserName, created.TemporaryPassword, "203.0.113.111"));
        Assert.Equal(SessionScope.ChangePassword, first.Scope);
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new StartMfaEnrolmentCommand(created.UserId)));

        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, first.SessionId, created.TemporaryPassword, new SecretString(MfaTestHelpers.Password)));
        var access = first.Tokens!.Access.Reveal();
        Assert.Equal(SessionScope.EnrolMfa, (await host.SendAsync(new AuthenticateSessionQuery(access)))!.Scope);

        // Enrolling moves the same session to the full scope, and the next sign-in asks for the second step.
        await MfaTestHelpers.EnrolAsync(host, created.UserId);
        Assert.Equal(SessionScope.Full, (await host.SendAsync(new AuthenticateSessionQuery(access)))!.Scope);
        var next = await host.SendAsync(new SignInCommand(created.UserName, new SecretString(MfaTestHelpers.Password), "203.0.113.111"));
        Assert.Equal(SignInStatus.PasswordAccepted, next.Status);
        Assert.NotNull(next.PendingToken);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task AWrongOrMalformedCode_ActivatesNothing()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, _) = await MfaTestHelpers.ReadyToEnrolAsync(host);
        var started = await host.SendAsync(new StartMfaEnrolmentCommand(userId));
        var secret = MfaTestHelpers.SecretOf(started);
        var wrong = MfaTestHelpers.CodeNow(host, secret) == "000000" ? "000001" : "000000";

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ConfirmMfaEnrolmentCommand(userId, wrong)));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ConfirmMfaEnrolmentCommand(userId, "12ab")));
        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new ConfirmMfaEnrolmentCommand(userId, string.Empty)));

        Assert.False((await LoadAsync(host, userId)).IsMfaEnrolled);
        Assert.Empty(await CodesOfAsync(host, userId));
        Assert.Single(await PendingOfAsync(host, userId)); // the person can still finish with the right code
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task AnExpiredEnrolment_CannotBeConfirmed()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, _) = await MfaTestHelpers.ReadyToEnrolAsync(host);
        var started = await host.SendAsync(new StartMfaEnrolmentCommand(userId));
        var secret = MfaTestHelpers.SecretOf(started);

        host.Time.Now += MfaEnrolmentPending.Lifetime + TimeSpan.FromSeconds(1);

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new ConfirmMfaEnrolmentCommand(userId, MfaTestHelpers.CodeNow(host, secret))));
        Assert.False((await LoadAsync(host, userId)).IsMfaEnrolled);
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task AnInactiveAccount_CannotEnrol()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, _) = await MfaTestHelpers.ReadyToEnrolAsync(host);
        await host.SendAsync(new DeactivateStaffCommand(userId));

        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new StartMfaEnrolmentCommand(userId)));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task Regenerate_InvalidatesOldCodes_AndNeedsACurrentCode()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, _) = await MfaTestHelpers.ReadyToEnrolAsync(host);
        var (secret, oldCodes) = await MfaTestHelpers.EnrolAsync(host, userId);

        // The code that confirmed enrolment (this step) is spent; a code for the next step proves the person still holds the device.
        await Assert.ThrowsAsync<DomainException>(() => host.SendAsync(new RegenerateRecoveryCodesCommand(userId, MfaTestHelpers.CodeNow(host, secret))));
        var fresh = await host.SendAsync(new RegenerateRecoveryCodesCommand(userId, MfaTestHelpers.CodeNow(host, secret, 1)));

        Assert.Equal(10, fresh.Codes.Count);
        Assert.Empty(fresh.Codes.Select(c => c.Reveal()).Intersect(oldCodes));
        Assert.Equal(10, (await CodesOfAsync(host, userId)).Count); // replaced, not added to

        var pepper = new Romp.Modules.Identity.Infrastructure.Mfa.ConfiguredRecoveryPepper(Microsoft.Extensions.Options.Options.Create(new Romp.Modules.Identity.Infrastructure.Mfa.MfaOptions())).Value;
        var rows = await CodesOfAsync(host, userId);
        Assert.Null(RecoveryCodes.TryUse(rows, userId, pepper, oldCodes[0], host.Time.Now));
        Assert.NotNull(RecoveryCodes.TryUse(rows, userId, pepper, fresh.Codes[0].Reveal(), host.Time.Now));
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task TheResultsPrint_AsMasks_NotSecrets()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, _) = await MfaTestHelpers.ReadyToEnrolAsync(host);

        var started = await host.SendAsync(new StartMfaEnrolmentCommand(userId));
        var issued = await host.SendAsync(new ConfirmMfaEnrolmentCommand(userId, MfaTestHelpers.CodeNow(host, MfaTestHelpers.SecretOf(started))));

        var printed = started.ToString() + issued;
        Assert.DoesNotContain(started.SecretText.Reveal(), printed, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(started.OtpAuthUri.Reveal()), printed, StringComparison.Ordinal);
        Assert.All(issued.Codes, c => Assert.DoesNotContain(c.Reveal(), printed, StringComparison.Ordinal));
        Assert.Throws<NotSupportedException>(() => System.Text.Json.JsonSerializer.Serialize(started));
        Assert.Throws<NotSupportedException>(() => System.Text.Json.JsonSerializer.Serialize(issued));
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task TheRealReset_ClearsTheSecondFactor_TheCodesAndAnyPendingEnrolment()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres);
        var (userId, _) = await MfaTestHelpers.ReadyToEnrolAsync(host);
        await MfaTestHelpers.EnrolAsync(host, userId);
        await host.SendAsync(new GetMfaStatusQuery(userId));

        await using (var scope = host.Scope())
        {
            await scope.ServiceProvider.GetRequiredService<IMfaEnrolmentReset>().ClearAsync(userId, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IdntDbContext>().SaveChangesAsync();
        }

        Assert.False((await LoadAsync(host, userId)).IsMfaEnrolled);
        Assert.Empty(await CodesOfAsync(host, userId));
        Assert.Equal(new MfaStatus(false, 0), await host.SendAsync(new GetMfaStatusQuery(userId)));
    }
}
