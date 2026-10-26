using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Application;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Sessions;

[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class RefreshSessionTests(IdentityPostgres postgres)
{
    private static async Task<(long UserId, IssuedSession Issued)> SignedInAsync(IdentityHost host, string ip)
    {
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        var issued = await host.SendAsync(new IssueSessionCommand(created.UserId, null, ip, "agent"));
        return (created.UserId, issued);
    }

    private static async Task<Session> LoadAsync(IdentityHost host, long sessionId)
    {
        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        return await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Refresh_IssuesANewPair_AndStoresOnlyTheirHashes()
    {
        using var host = new IdentityHost(postgres);
        var (_, issued) = await SignedInAsync(host, "203.0.113.70");
        host.Time.Now += TimeSpan.FromMinutes(10);

        var result = await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal()));

        Assert.Equal(RefreshOutcome.Rotated, result.Outcome);
        var fresh = result.Session!;
        Assert.Equal(issued.SessionId, fresh.SessionId);
        Assert.NotEqual(issued.Tokens.Access.Reveal(), fresh.Tokens.Access.Reveal());
        Assert.NotEqual(issued.Tokens.Refresh.Reveal(), fresh.Tokens.Refresh.Reveal());
        Assert.Equal(host.Time.Now.AddMinutes(15), fresh.AccessExpiresAt);
        Assert.Equal(host.Time.Now.AddMinutes(30), fresh.RefreshExpiresAt);

        var stored = await LoadAsync(host, issued.SessionId);
        Assert.Equal(SessionTokens.Hash(fresh.Tokens.Refresh.Reveal()), stored.RefreshTokenHash);
        Assert.True(stored.AccessTokenMatches(fresh.Tokens.Access.Reveal()));
        Assert.False(stored.AccessTokenMatches(issued.Tokens.Access.Reveal()));
        Assert.Equal(host.Time.Now, stored.RotatedAt);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Refresh_UsesTheConfiguredLifetimes_NotTheDefaults()
    {
        var custom = new SessionLifetimes(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1));
        using var host = new IdentityHost(postgres, configure: services => services.AddSingleton(custom));
        var (_, issued) = await SignedInAsync(host, "203.0.113.71");
        host.Time.Now += TimeSpan.FromMinutes(3);

        var result = await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal()));

        Assert.Equal(host.Time.Now.AddMinutes(5), result.Session!.AccessExpiresAt);
        Assert.Equal(host.Time.Now.AddMinutes(10), result.Session.RefreshExpiresAt);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task TwoParallelRefreshes_OneWins_NoRevocation()
    {
        using var host = new IdentityHost(postgres);
        var (_, issued) = await SignedInAsync(host, "203.0.113.72");
        host.Time.Now += TimeSpan.FromMinutes(1);
        var value = issued.Tokens.Refresh.Reveal();

        var results = await Task.WhenAll(
            host.SendAsync(new RefreshSessionCommand(value)),
            host.SendAsync(new RefreshSessionCommand(value)));

        var winner = Assert.Single(results, r => r.Outcome == RefreshOutcome.Rotated);
        var loser = Assert.Single(results, r => r.Outcome == RefreshOutcome.ConcurrentRefresh);
        Assert.Null(loser.Session); // the loser gets no cookies; the winner's response sets them

        var stored = await LoadAsync(host, issued.SessionId);
        Assert.Null(stored.RevokedAt);
        Assert.Equal(SessionTokens.Hash(winner.Session!.Tokens.Refresh.Reveal()), stored.RefreshTokenHash);

        // The winner's pair keeps working: the next refresh is an ordinary rotation.
        host.Time.Now += TimeSpan.FromMinutes(1);
        var next = await host.SendAsync(new RefreshSessionCommand(winner.Session.Tokens.Refresh.Reveal()));
        Assert.Equal(RefreshOutcome.Rotated, next.Outcome);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task ManyParallelRefreshes_StillHaveExactlyOneWinner()
    {
        using var host = new IdentityHost(postgres);
        var (_, issued) = await SignedInAsync(host, "203.0.113.73");
        host.Time.Now += TimeSpan.FromMinutes(1);
        var value = issued.Tokens.Refresh.Reveal();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => host.SendAsync(new RefreshSessionCommand(value))));

        Assert.Equal(1, results.Count(r => r.Outcome == RefreshOutcome.Rotated));
        Assert.Equal(7, results.Count(r => r.Outcome == RefreshOutcome.ConcurrentRefresh));
        Assert.Null((await LoadAsync(host, issued.SessionId)).RevokedAt);
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task OldValueWithinTheGrace_IsRefused_AndTheSessionSurvives()
    {
        using var host = new IdentityHost(postgres);
        var (_, issued) = await SignedInAsync(host, "203.0.113.74");
        host.Time.Now += TimeSpan.FromMinutes(1);
        var winner = await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal()));

        host.Time.Now += TimeSpan.FromSeconds(9);
        var late = await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal()));

        Assert.Equal(RefreshOutcome.ConcurrentRefresh, late.Outcome);
        var stored = await LoadAsync(host, issued.SessionId);
        Assert.Null(stored.RevokedAt);
        Assert.Equal(SessionTokens.Hash(winner.Session!.Tokens.Refresh.Reveal()), stored.RefreshTokenHash);
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task OldValueAfterGrace_RevokesTheSession()
    {
        using var host = new IdentityHost(postgres);
        var (_, issued) = await SignedInAsync(host, "203.0.113.75");
        host.Time.Now += TimeSpan.FromMinutes(1);
        var winner = await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal()));

        host.Time.Now += TimeSpan.FromSeconds(11);
        var replay = await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal()));

        Assert.Equal(RefreshOutcome.ReuseDetected, replay.Outcome);
        Assert.Null(replay.Session);
        var stored = await LoadAsync(host, issued.SessionId);
        Assert.Equal(host.Time.Now, stored.RevokedAt);
        Assert.Equal(SessionRevokeReason.RefreshReuse, stored.RevokeReason);

        // The legitimate holder is signed out too: the newest value is dead.
        var afterwards = await host.SendAsync(new RefreshSessionCommand(winner.Session!.Tokens.Refresh.Reveal()));
        Assert.Equal(RefreshOutcome.Unusable, afterwards.Outcome);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task UnknownRevokedAndExpiredValues_AreUnusable_AndChangeNothing()
    {
        using var host = new IdentityHost(postgres);
        var (_, issued) = await SignedInAsync(host, "203.0.113.76");

        var unknown = await host.SendAsync(new RefreshSessionCommand(SessionTokens.Generate().Reveal()));
        Assert.Equal(RefreshOutcome.Unusable, unknown.Outcome);
        Assert.Null((await LoadAsync(host, issued.SessionId)).RevokedAt);

        host.Time.Now += TimeSpan.FromMinutes(31); // past the idle window
        var idle = await host.SendAsync(new RefreshSessionCommand(issued.Tokens.Refresh.Reveal()));
        Assert.Equal(RefreshOutcome.Unusable, idle.Outcome);

        // A session ended by a new sign-in cannot be refreshed.
        var (userId, other) = await SignedInAsync(host, "203.0.113.77");
        await host.SendAsync(new IssueSessionCommand(userId, other.Tokens.Access.Reveal(), "203.0.113.77", null));
        var revoked = await host.SendAsync(new RefreshSessionCommand(other.Tokens.Refresh.Reveal()));
        Assert.Equal(RefreshOutcome.Unusable, revoked.Outcome);
    }

    [Theory]
    [Trait("Spec", "AC-2")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyValues_AreRejectedBeforeTheDatabase(string value)
    {
        using var host = new IdentityHost(postgres);

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new RefreshSessionCommand(value)));
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task OversizedValues_AreRejectedBeforeTheDatabase()
    {
        using var host = new IdentityHost(postgres);

        await Assert.ThrowsAsync<ValidationException>(() => host.SendAsync(new RefreshSessionCommand(new string('a', 129))));
    }
}
