using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.Sessions;

/// <summary>Sessions task 14: sign-in history is kept for 90 days, then removed; nothing newer is touched.</summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class PurgeExpiredSessionsJobTests(IdentityPostgres postgres)
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task RemovesOnlyOldRows()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        var now = host.Time.Now;
        var name = "purge." + Guid.NewGuid().ToString("N")[..10];

        // Sessions are judged by when they could last have been used (the absolute limit), which is 12 hours after they started.
        var oldEnded = Session.Start(created.UserId, now - Retention - TimeSpan.FromDays(1), SessionLifetimes.Default).Session;
        var oldRevoked = Session.Start(created.UserId, now - Retention - TimeSpan.FromDays(5), SessionLifetimes.Default).Session;
        oldRevoked.Revoke(SessionRevokeReason.SignOut, now - Retention - TimeSpan.FromDays(5));
        var recentEnded = Session.Start(created.UserId, now - Retention + TimeSpan.FromDays(1), SessionLifetimes.Default).Session;
        var justRevoked = Session.Start(created.UserId, now - TimeSpan.FromHours(2), SessionLifetimes.Default).Session;
        justRevoked.Revoke(SessionRevokeReason.SignOut, now - TimeSpan.FromHours(1));
        var active = Session.Start(created.UserId, now, SessionLifetimes.Default).Session;

        await using (var scope = host.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
            db.Sessions.AddRange(oldEnded, oldRevoked, recentEnded, justRevoked, active);
            db.LoginAttempts.Add(LoginAttempt.Record(name, "203.0.113.99", LoginOutcome.BadCredentials, null, now - Retention - TimeSpan.FromDays(2)));
            db.LoginAttempts.Add(LoginAttempt.Record(name, "203.0.113.99", LoginOutcome.BadCredentials, null, now - Retention + TimeSpan.FromDays(2)));
            db.LoginAttempts.Add(LoginAttempt.Record(name, "203.0.113.99", LoginOutcome.Ok, created.UserId, now));
            var (oldPending, _) = PendingSignIn.Start(created.UserId, now - Retention - TimeSpan.FromDays(3));
            var (newPending, _) = PendingSignIn.Start(created.UserId, now - TimeSpan.FromMinutes(1));
            db.PendingSignIns.AddRange(oldPending, newPending);
            await db.SaveChangesAsync();
        }

        var result = await host.SendAsync(new PurgeExpiredSessionsCommand());

        await using var read = host.Scope();
        var context = read.ServiceProvider.GetRequiredService<IdntDbContext>();
        var remaining = await context.Sessions.AsNoTracking().Where(s => s.UserId == created.UserId).Select(s => s.Id).ToListAsync();
        Assert.DoesNotContain(oldEnded.Id, remaining);
        Assert.DoesNotContain(oldRevoked.Id, remaining);
        Assert.Contains(recentEnded.Id, remaining);
        Assert.Contains(justRevoked.Id, remaining);
        Assert.Contains(active.Id, remaining);

        var attempts = await context.LoginAttempts.AsNoTracking().Where(a => a.AttemptName == name).Select(a => a.AttemptedAt).ToListAsync();
        Assert.Equal(2, attempts.Count);
        Assert.All(attempts, at => Assert.True(at > now - Retention));

        var pending = await context.PendingSignIns.AsNoTracking().Where(p => p.UserId == created.UserId).CountAsync();
        Assert.Equal(1, pending);

        Assert.True(result.Sessions >= 2);
        Assert.True(result.Attempts >= 1);
        Assert.True(result.PendingSignIns >= 1);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task RunningTwice_RemovesNothingMore()
    {
        using var host = new IdentityHost(postgres);
        await host.SendAsync(new PurgeExpiredSessionsCommand());

        var again = await host.SendAsync(new PurgeExpiredSessionsCommand());

        Assert.Equal(new PurgeResult(0, 0, 0), again);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public void TheRetention_IsNinetyDays()
    {
        Assert.Equal(TimeSpan.FromDays(90), PurgeExpiredSessionsCommandHandler.Retention);
    }
}
