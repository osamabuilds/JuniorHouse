using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Web;
using Romp.Modules.Identity.Application.Mfa;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Mfa;

/// <summary>MFA task 9 (AC-9): the operator's break-glass reset of an Owner leads to a fresh enrolment and new recovery codes.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class MfaOwnerResetTests(IdentityPostgres postgres)
{
    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task OwnerBreakGlassReset_ForcesReEnrolment_AndNewRecoveryCodesAreShownOnce()
    {
        using var host = MfaTestHelpers.RealMfaHost(postgres, services => services.AddScoped<IUserSessionRevoker, SessionRevoker>());
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Owner));
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, null, created.TemporaryPassword, new SecretString(MfaTestHelpers.Password)));
        var (_, oldCodes) = await MfaTestHelpers.EnrolAsync(host, created.UserId);
        host.Time.Now += TimeSpan.FromSeconds(31);
        var session = await host.SendAsync(new IssueSessionCommand(created.UserId, null, "203.0.113.170", null));

        await host.SendAsync(new ResetOwnerCommand(created.UserName, null, ClearMfa: true)); // the operator step: only the second factor

        Assert.False((await host.LoadAsync(created.UserId)).IsMfaEnrolled);
        Assert.Null(await host.SendAsync(new AuthenticateSessionQuery(session.Tokens.Access.Reveal()))); // every session ended

        var again = await host.SendAsync(new SignInCommand(created.UserName, new SecretString(MfaTestHelpers.Password), "203.0.113.171"));
        Assert.Equal(SignInStatus.LimitedSession, again.Status);
        Assert.Equal(SessionScope.EnrolMfa, again.Scope);

        // The Owner enrols again and gets a new set of codes, once.
        var (_, newCodes) = await MfaTestHelpers.EnrolAsync(host, created.UserId);
        Assert.Equal(10, newCodes.Count);
        Assert.Empty(newCodes.Intersect(oldCodes));
    }
}

/// <summary>MFA task 10 (AC-10): there is no switch, in code or in configuration, that turns the second factor off.</summary>
public sealed class MfaPolicyTests
{
    private static readonly Regex Subject = new("mfa|two.?step|second.?factor|totp|otp|multi.?factor", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Switch = new("enabl|disabl|off|skip|bypass|optional|require|exempt|waive|allow.?without|none", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (CLAUDE.md) not found.");
    }

    private static bool LooksLikeAnMfaSwitch(string name) => Subject.IsMatch(name) && Switch.IsMatch(name);

    [Fact]
    [Trait("Spec", "AC-10")]
    public void NoConfigurationKey_DisablesMfa()
    {
        // Every settings class of the host and of Identity: no property may read like a way to turn the second factor off or optional.
        var assemblies = new[]
        {
            typeof(Romp.Modules.Identity.Infrastructure.IdentityModule).Assembly,
            typeof(Romp.Modules.Identity.Application.AssemblyReference).Assembly,
            typeof(IdentityOptions).Assembly,
        };
        var offenders = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && t.IsPublic && (t.Name.EndsWith("Options", StringComparison.Ordinal) || t.Name.EndsWith("Settings", StringComparison.Ordinal)))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => $"{t.Name}.{p.Name}"))
            .Where(LooksLikeAnMfaSwitch)
            .ToList();

        Assert.True(offenders.Count == 0, "Settings that could switch MFA off: " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void NoSettingsFile_ContainsAnMfaSwitch()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "api", "src", "Romp.Api");
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(folder, "appsettings*.json"))
        {
            foreach (Match key in Regex.Matches(File.ReadAllText(file), "\"([^\"]+)\"\\s*:"))
            {
                if (LooksLikeAnMfaSwitch(key.Groups[1].Value))
                {
                    offenders.Add($"{Path.GetFileName(file)}: {key.Groups[1].Value}");
                }
            }
        }

        var compose = File.ReadAllText(Path.Combine(RepositoryRoot(), "docker-compose.yml"));
        offenders.AddRange(Regex.Matches(compose, @"^\s*([A-Za-z0-9_]+):", RegexOptions.Multiline).Select(m => m.Groups[1].Value).Where(LooksLikeAnMfaSwitch).Select(k => "docker-compose.yml: " + k));

        Assert.True(offenders.Count == 0, "MFA switches in configuration: " + string.Join(", ", offenders));
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public void TheDetector_RecognisesSwitchesAndLeavesTheKeysAlone()
    {
        foreach (var bad in new[] { "MfaEnabled", "DisableMfa", "RequireMfa", "Mfa:Optional", "SkipTwoStep", "Totp.Bypass", "SecondFactorOff" })
        {
            Assert.True(LooksLikeAnMfaSwitch(bad), bad);
        }

        foreach (var good in new[] { "KeyId", "EncryptionKey", "RecoveryPepper", "PreviousKeys", "Argon2", "Csrf", "AccessMinutes" })
        {
            Assert.False(LooksLikeAnMfaSwitch(good), good);
        }
    }

    [Theory]
    [Trait("Spec", "AC-10")]
    [InlineData(false, false, SessionScope.EnrolMfa)]
    [InlineData(false, true, SessionScope.Full)]
    public void OnlyAnEnrolledAccount_EverGetsTheFullScope(bool mustChangePassword, bool enrolled, SessionScope expected)
    {
        Assert.Equal(expected, SessionScopes.For(mustChangePassword, enrolled));
        Assert.NotEqual(SessionScope.Full, SessionScopes.For(mustChangePassword, mfaEnrolled: false));
        Assert.NotEqual(SessionScope.Full, SessionScopes.For(mustChangePassword: true, mfaEnrolled: true));
    }
}
