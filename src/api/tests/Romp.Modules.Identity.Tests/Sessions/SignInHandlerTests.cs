using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
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

/// <summary>Counts password checks, to prove that every failure kind pays for exactly one hash verification (sessions AC-7).</summary>
internal sealed class CountingPasswordHasher(IPasswordHasher inner) : IPasswordHasher
{
    public int VerifyCalls { get; set; }

    public string Hash(SecretString password) => inner.Hash(password);

    public bool Verify(SecretString password, string storedHash)
    {
        VerifyCalls++;
        return inner.Verify(password, storedHash);
    }

    public bool NeedsRehash(string storedHash) => inner.NeedsRehash(storedHash);
}

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class SignInHandlerTests(IdentityPostgres postgres)
{
    private const string Ip = "203.0.113.10";
    private const string GoodPassword = "Correct-Horse-Battery-Staple-42";

    private readonly CountingPasswordHasher _hasher = new(StaffHandlerHelpers.Hasher);

    private IdentityHost NewHost() => new(postgres, configure: services => services.AddSingleton<IPasswordHasher>(_hasher));

    /// <summary>An active account that has chosen its own password, so it is not on a temporary one.</summary>
    private static async Task<(long Id, string Name)> ActiveUserAsync(IdentityHost host)
    {
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, null, created.TemporaryPassword, new SecretString(GoodPassword)));
        host.Mfa.Enrolled.Add(created.UserId); // an enrolled person goes on to the MFA step; an unenrolled one gets a limited session
        return (created.UserId, created.UserName);
    }

    private static Task<SignInResult> SignInAsync(IdentityHost host, string name, string password, string ip = Ip) =>
        host.SendAsync(new SignInCommand(name, new SecretString(password), ip));

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task CorrectPassword_ReturnsAPendingToken_StoredOnlyAsAHash()
    {
        using var host = NewHost();
        var (id, name) = await ActiveUserAsync(host);

        var result = await SignInAsync(host, $"  {name.ToUpperInvariant()} ", GoodPassword);

        Assert.Equal(SignInStatus.PasswordAccepted, result.Status);
        Assert.Equal(id, result.UserId);
        Assert.NotNull(result.PendingToken);
        Assert.Null(result.RetryAfterSeconds);

        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        var pending = await db.PendingSignIns.AsNoTracking().SingleAsync(p => p.UserId == id);
        Assert.True(SessionTokens.Matches(result.PendingToken.Reveal(), pending.TokenHash));
        Assert.Equal(IdentityHost.Start + PendingSignIn.Lifetime, pending.ExpiresAt);
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task AllFailureKinds_LookIdentical()
    {
        using var host = NewHost();
        var (_, active) = await ActiveUserAsync(host);
        var (inactiveId, inactive) = await ActiveUserAsync(host);
        await host.SendAsync(new DeactivateStaffCommand(inactiveId));
        var expired = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        host.Time.Now = IdentityHost.Start + StaffUser.TemporaryPasswordLifetime + TimeSpan.FromHours(1);

        var results = new List<SignInResult>();
        foreach (var (name, password, ip) in new[]
        {
            (active, "wrong-password-entirely", "198.51.100.1"),
            ("nobody.at.all", GoodPassword, "198.51.100.2"),
            (inactive, GoodPassword, "198.51.100.3"),
            (expired.UserName, expired.TemporaryPassword.Reveal(), "198.51.100.4"),
            (active, string.Empty, "198.51.100.5"),
            ("x", GoodPassword, "198.51.100.6"), // not even a valid user name
        })
        {
            _hasher.VerifyCalls = 0;
            results.Add(await SignInAsync(host, name, password, ip));
            Assert.Equal(1, _hasher.VerifyCalls); // every kind pays for one verification, so timing does not tell them apart
        }

        Assert.All(results, r => Assert.Equal(SignInResult.Failure, r));
        Assert.Equal(SignInStatus.Failed, results[0].Status);
        Assert.Null(results[0].PendingToken);
        Assert.Null(results[0].UserId);
    }

    [Fact]
    [Trait("Spec", "AC-7")]
    public async Task UnknownUser_StillPaysTheHashCost()
    {
        using var host = NewHost();
        _hasher.VerifyCalls = 0;

        var result = await SignInAsync(host, "does.not.exist." + IdentityHost.NewUserName(), GoodPassword);

        Assert.Equal(SignInResult.Failure, result);
        Assert.Equal(1, _hasher.VerifyCalls);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task TemporaryPassword_WorksExactlyOnce()
    {
        using var host = NewHost();
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));

        var first = await SignInAsync(host, created.UserName, created.TemporaryPassword.Reveal());
        var second = await SignInAsync(host, created.UserName, created.TemporaryPassword.Reveal(), "198.51.100.77");

        Assert.Equal(SignInStatus.LimitedSession, first.Status); // must change the password before anything else
        Assert.Equal(SignInResult.Failure, second);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task LogsEveryAttempt_WithoutThePassword()
    {
        using var host = NewHost();
        var (id, name) = await ActiveUserAsync(host);
        var unknown = "\u0007Ghost\nName" + new string('x', 60);
        var secret = "Never-Log-This-Password-" + Guid.NewGuid().ToString("N");

        await SignInAsync(host, name, secret, "198.51.100.20");
        await SignInAsync(host, name, GoodPassword, "198.51.100.21");
        await SignInAsync(host, unknown, secret, "198.51.100.22");

        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        var rows = await db.LoginAttempts.AsNoTracking()
            .Where(a => a.ClientIp.StartsWith("198.51.100.2") && (a.UserId == id || a.UserId == null && a.AttemptName.StartsWith("ghostname")))
            .OrderBy(a => a.Id).ToListAsync();

        Assert.Equal([LoginOutcome.BadCredentials, LoginOutcome.Ok, LoginOutcome.BadCredentials], rows.Select(r => r.Outcome));
        Assert.Equal(id, rows[0].UserId);
        Assert.Null(rows[2].UserId);
        Assert.Equal(name, rows[0].AttemptName);
        Assert.All(rows, r => Assert.Equal(IdentityHost.Start, r.AttemptedAt));
        Assert.StartsWith("ghostname", rows[2].AttemptName, StringComparison.Ordinal); // control characters removed
        Assert.Equal(LoginAttempt.NameMaxLength, rows[2].AttemptName.Length);          // truncated, never rejected
        Assert.All(rows, r => Assert.DoesNotContain("Never-Log-This", $"{r.AttemptName}|{r.ClientIp}|{r.Outcome}|{r.InsrBy}", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task Throttles_PerPairAndPerName()
    {
        using var host = NewHost();
        var (_, name) = await ActiveUserAsync(host);

        // Two failures are free; the third starts a 30 s delay for this name and address only.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(SignInResult.Failure, await SignInAsync(host, name, "bad-password-" + i));
        }

        var blocked = await SignInAsync(host, name, GoodPassword); // even the right password waits
        Assert.Equal(SignInStatus.Throttled, blocked.Status);
        Assert.Equal(30, blocked.RetryAfterSeconds);
        Assert.Null(blocked.PendingToken);

        // Another address is not delayed (the throttle is per pair) ...
        Assert.Equal(SignInStatus.PasswordAccepted, (await SignInAsync(host, name, GoodPassword, "198.51.100.31")).Status);

        // ... and a throttled attempt costs no hash and does not extend the wait.
        host.Time.Now += TimeSpan.FromSeconds(10);
        _hasher.VerifyCalls = 0;
        var again = await SignInAsync(host, name, GoodPassword);
        Assert.Equal(20, again.RetryAfterSeconds);
        Assert.Equal(0, _hasher.VerifyCalls);

        host.Time.Now += TimeSpan.FromSeconds(20);
        var allowed = await SignInAsync(host, name, GoodPassword);
        Assert.Equal(SignInStatus.PasswordAccepted, allowed.Status);

        // A success resets the count: the next failure is free again.
        Assert.Equal(SignInResult.Failure, await SignInAsync(host, name, "bad-again"));
        Assert.Equal(SignInStatus.PasswordAccepted, (await SignInAsync(host, name, GoodPassword)).Status);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public async Task GlobalCeiling_AppliesToTheNameFromAnyAddress_ForUnknownNamesToo()
    {
        using var host = NewHost();
        var (_, name) = await ActiveUserAsync(host);

        for (var i = 0; i < ThrottlePolicy.GlobalCeilingFailures; i++)
        {
            Assert.Equal(SignInResult.Failure, await SignInAsync(host, name, "guess-" + i, $"192.0.2.{i + 1}"));
        }

        var fresh = await SignInAsync(host, name, GoodPassword, "192.0.2.200");
        Assert.Equal(SignInStatus.Throttled, fresh.Status);
        Assert.Equal((int)ThrottlePolicy.GlobalDelay.TotalSeconds, fresh.RetryAfterSeconds);

        // It always ends: the account is never locked.
        host.Time.Now += ThrottlePolicy.GlobalDelay + TimeSpan.FromSeconds(1);
        Assert.Equal(SignInStatus.PasswordAccepted, (await SignInAsync(host, name, GoodPassword, "192.0.2.201")).Status);

        // An unknown name is throttled the same way, so the 429 does not reveal whether it exists.
        var ghost = "ghost." + IdentityHost.NewUserName();
        for (var i = 0; i < ThrottlePolicy.GlobalCeilingFailures; i++)
        {
            await SignInAsync(host, ghost, "guess", $"192.0.2.{i + 20}");
        }

        Assert.Equal(SignInStatus.Throttled, (await SignInAsync(host, ghost, "guess", "192.0.2.250")).Status);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task ARequestWithoutAClientAddress_IsRefusedByValidation()
    {
        using var host = NewHost();

        await Assert.ThrowsAsync<ValidationException>(() => SignInAsync(host, "someone", GoodPassword, string.Empty));
        await Assert.ThrowsAsync<ValidationException>(() => SignInAsync(host, "someone", GoodPassword, new string('1', 46)));
    }
}
