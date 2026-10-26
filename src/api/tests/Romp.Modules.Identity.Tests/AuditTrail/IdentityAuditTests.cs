using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Romp.BuildingBlocks.Auditing;
using Romp.BuildingBlocks.Persistence.Auditing;
using Romp.BuildingBlocks.Persistence.Outbox;
using Romp.Modules.Identity.Application.Mfa;
using Romp.Modules.Identity.Application.Roles;
using Romp.Modules.Identity.Application.Sessions;
using Romp.Modules.Identity.Application.StaffAccounts;
using Romp.Modules.Identity.Domain.AuditTrail;
using Romp.Modules.Identity.Domain.Roles;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Infrastructure.AuditTrail;
using Romp.Modules.Identity.Infrastructure.Persistence;
using Romp.Modules.Identity.Tests.Roles;
using Romp.Modules.Identity.Tests.StaffAccounts;
using Romp.Modules.Identity.Tests.Support;

namespace Romp.Modules.Identity.Tests.AuditTrail;

/// <summary>Plays the dispatcher for the audit messages in Identity's own outbox: hands each one to the real handler, then marks it processed.</summary>
internal static class AuditDelivery
{
    public static async Task<int> DeliverPendingAsync(IdentityHost host)
    {
        await using var scope = host.Scope();
        var db = scope.ServiceProvider.GetRequiredService<IdntDbContext>();
        var rows = await db.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.EventType == nameof(AuditRecorded) && m.ProcessedDte == null)
            .OrderBy(m => m.Id).ToListAsync();

        foreach (var row in rows)
        {
            await DeliverAsync(host, row);
            await using var mark = host.Scope();
            await mark.ServiceProvider.GetRequiredService<IdntDbContext>().Database
                .ExecuteSqlAsync($"""UPDATE "IDNT"."OUTB_MSG" SET "PROC_DTE" = now() WHERE "ID" = {row.Id}""");
        }

        return rows.Count;
    }

    /// <summary>One delivery attempt of one outbox row, exactly as the dispatcher makes it (a fresh scope, the message context filled in).</summary>
    public static async Task DeliverAsync(IdentityHost host, OutboxMessage row)
    {
        var message = JsonSerializer.Deserialize<AuditRecorded>(row.Payload)!;
        await using var scope = host.Scope();
        var context = scope.ServiceProvider.GetRequiredService<OutboxMessageContext>();
        context.MessageId = row.Id;
        context.Schema = "IDNT";
        var handler = ActivatorUtilities.CreateInstance<RecordAuditHandler>(scope.ServiceProvider);
        await handler.HandleAsync(message, CancellationToken.None);
    }

    public static async Task<List<AuditEntry>> EntriesAsync(IdentityPostgres postgres, string? actor = null, string? entityType = null)
    {
        await using var db = postgres.NewContext();
        var query = db.AuditEntries.AsNoTracking().Include(e => e.Columns).AsQueryable();
        if (actor is not null)
        {
            query = query.Where(e => e.Actor == actor);
        }

        if (entityType is not null)
        {
            query = query.Where(e => e.EntityType == entityType);
        }

        return await query.OrderBy(e => e.Id).ToListAsync();
    }
}

/// <summary>
/// Identity's own changes are in the trail (identity/audit-trail AC-7, task 9): accounts, roles, MFA, sign-in outcomes, deactivation and
/// the operator break-glass steps, each attributed to who did it. Runs the real command pipeline, the real collector and the real
/// handler against PostgreSQL.
/// </summary>
[Collection(IdentityPostgresFixtureDefinition.Name)]
public sealed class IdentityAuditTests(IdentityPostgres postgres)
{
    private const string GoodPassword = "Correct-Horse-Battery-Staple-42";

    [Fact]
    [Trait("Spec", "FR-59")]
    [Trait("Spec", "AC-7")]
    public async Task AccountRoleAndMfaChanges_AreAudited_WithWhoDidThem()
    {
        using var host = new IdentityHost(postgres);
        var owner = $"owner.{IdentityHost.NewUserName()}";
        var catalogue = await host.RoleIdAsync(RoleCodes.Catalogue);
        var orderOps = await host.RoleIdAsync(RoleCodes.OrderOps);

        long userId;
        using (ActorScope.Begin(owner))
        {
            userId = (await StaffHandlerHelpers.CreateAsync(host, catalogue)).UserId;
            await host.SendAsync(new AssignRolesCommand(userId, [catalogue, orderOps]));
            await host.SendAsync(new ResetStaffMfaCommand(userId, ActingUserId: 999_999));
            await host.SendAsync(new DeactivateStaffCommand(userId));
        }

        await AuditDelivery.DeliverPendingAsync(host);
        var mine = await AuditDelivery.EntriesAsync(postgres, actor: owner);

        var created = Assert.Single(mine, e => e.EntityType == "USR_MAIN" && e.Operation == AuditOperation.Insert);
        Assert.Equal(userId.ToString(System.Globalization.CultureInfo.InvariantCulture), created.EntityKey);
        Assert.Equal("IDNT", created.Module);

        // Role changes: a mapping row per role, keyed by person and role.
        var roleRows = mine.Where(e => e.EntityType == "USR_ROLE_MAP").ToList();
        Assert.Contains(roleRows, e => e.Operation == AuditOperation.Insert && e.EntityKey == FormattableString.Invariant($"{userId}|{orderOps}"));

        // The deactivation is a change of the account row naming the status column.
        var updates = mine.Where(e => e.EntityType == "USR_MAIN" && e.Operation == AuditOperation.Update && e.EntityKey == created.EntityKey).ToList();
        Assert.Contains(updates, e => e.Columns.Any(c => c.Name == "ACT_IND"));
        Assert.All(mine, e => Assert.Equal(owner, e.Actor));
    }

    [Fact]
    [Trait("Spec", "FR-59")]
    [Trait("Spec", "AC-7")]
    public async Task SignInFailures_AreAudited_AsAttemptRows()
    {
        using var host = new IdentityHost(postgres);
        var created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, null, created.TemporaryPassword, new SecretString(GoodPassword)));

        await host.SendAsync(new SignInCommand(created.UserName, new SecretString("Wrong-Password-For-Sure-123"), "203.0.113.77"));
        await AuditDelivery.DeliverPendingAsync(host);

        var attempts = await AuditDelivery.EntriesAsync(postgres, entityType: "LGIN_ATMP");
        var attempt = Assert.Single(attempts, e => e.Columns.Any(c => c.Name == "USR_ID") && e.Operation == AuditOperation.Insert && e.Actor == "system");
        Assert.Contains("OTCM_ID", attempt.Columns.Select(c => c.Name));

        // The attempt row itself says which person and which outcome; the audit entry points at it by key.
        await using var db = postgres.NewContext();
        var row = await db.LoginAttempts.AsNoTracking().SingleAsync(a => a.Id == long.Parse(attempt.EntityKey, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(created.UserId, row.UserId);
        Assert.Equal(LoginOutcome.BadCredentials, row.Outcome);
    }

    [Fact]
    [Trait("Spec", "FR-59")]
    [Trait("Spec", "AC-7")]
    public async Task OperatorBreakGlass_IsAuditedAsOperatorCli()
    {
        using var host = new IdentityHost(postgres);
        var name = IdentityHost.NewUserName();
        using (ActorScope.Begin("operator-cli"))
        {
            await host.SendAsync(new CreateOwnerCommand(name, "Break Glass", new SecretString(GoodPassword)));
        }

        await AuditDelivery.DeliverPendingAsync(host);
        var before = (await AuditDelivery.EntriesAsync(postgres, actor: "operator-cli", entityType: "USR_MAIN")).Count;

        using (ActorScope.Begin("operator-cli"))
        {
            await host.SendAsync(new ResetOwnerCommand(name, new SecretString("A-Brand-New-Recovery-Passphrase-77"), ClearMfa: true));
        }

        await AuditDelivery.DeliverPendingAsync(host);
        var after = await AuditDelivery.EntriesAsync(postgres, actor: "operator-cli", entityType: "USR_MAIN");

        Assert.Equal(before + 1, after.Count);
        Assert.Equal(AuditOperation.Update, after[^1].Operation);
    }

    [Fact]
    [Trait("Spec", "SEC-25")]
    [Trait("Spec", "AC-6")]
    public async Task NoSecretValueOrSecretColumnName_ReachesTheTrail()
    {
        using var host = new IdentityHost(postgres);
        var actor = $"owner.{IdentityHost.NewUserName()}";
        TemporaryPasswordResult created;
        using (ActorScope.Begin(actor))
        {
            created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
            created = await host.SendAsync(new ResetPasswordCommand(created.UserId));
            await host.SendAsync(new ChangeOwnPasswordCommand(created.UserId, null, created.TemporaryPassword, new SecretString(GoodPassword)));
        }

        // The raw messages, before they are delivered: names and keys only.
        await using var read = postgres.NewContext();
        var payloads = await read.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.EventType == nameof(AuditRecorded)).Select(m => m.Payload).ToListAsync();
        payloads = payloads.Where(p => p.Contains(actor, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(payloads);
        Assert.All(payloads, p =>
        {
            Assert.DoesNotContain(created.TemporaryPassword.Reveal(), p, StringComparison.Ordinal);
            Assert.DoesNotContain(GoodPassword, p, StringComparison.Ordinal);
            Assert.DoesNotContain("argon2", p, StringComparison.OrdinalIgnoreCase);
        });

        await AuditDelivery.DeliverPendingAsync(host);
        var columns = (await AuditDelivery.EntriesAsync(postgres, actor: actor)).SelectMany(e => e.Columns).Select(c => c.Name).ToList();
        Assert.NotEmpty(columns);
        Assert.All(columns, name => Assert.DoesNotMatch(@"(^|_)(HASH|SECR|TOKN|PEPR|CIPH)(_|$)", name));
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task DeliveredTwice_StoresOnce()
    {
        using var host = new IdentityHost(postgres);
        var actor = $"owner.{IdentityHost.NewUserName()}";
        using (ActorScope.Begin(actor))
        {
            await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        }

        await using var read = postgres.NewContext();
        var rows = await read.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.EventType == nameof(AuditRecorded)).ToListAsync();
        rows = rows.Where(m => m.Payload.Contains(actor, StringComparison.Ordinal)).ToList();
        var row = Assert.Single(rows);

        await AuditDelivery.DeliverAsync(host, row);
        var once = (await AuditDelivery.EntriesAsync(postgres, actor: actor)).Count;
        await AuditDelivery.DeliverAsync(host, row);
        var twice = (await AuditDelivery.EntriesAsync(postgres, actor: actor)).Count;

        Assert.True(once > 0);
        Assert.Equal(once, twice);
    }

    [Fact]
    [Trait("Spec", "AC-13")]
    [Trait("Spec", "NFR-FT-08")]
    public async Task AFailingHandler_StoresNothing_AndLeavesTheCommittedChangeAlone()
    {
        using var host = new IdentityHost(postgres);
        var actor = $"owner.{IdentityHost.NewUserName()}";
        TemporaryPasswordResult created;
        using (ActorScope.Begin(actor))
        {
            created = await StaffHandlerHelpers.CreateAsync(host, await host.RoleIdAsync(RoleCodes.Catalogue));
        }

        // A message this version cannot understand: the handler throws, so the dispatcher would retry and later dead-letter it.
        var broken = new AuditRecorded(
            Guid.NewGuid(), DateTimeOffset.UtcNow, "IDNT", actor, null, [new AuditRecord("USR_MAIN", "1", "EXPLODE", ["NAME"])]);
        await using (var scope = host.Scope())
        {
            var context = scope.ServiceProvider.GetRequiredService<OutboxMessageContext>();
            context.MessageId = 424_242;
            context.Schema = "IDNT";
            var handler = ActivatorUtilities.CreateInstance<RecordAuditHandler>(scope.ServiceProvider);
            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(broken, CancellationToken.None));
        }

        Assert.DoesNotContain(await AuditDelivery.EntriesAsync(postgres, actor: actor), e => e.SourceMessageId == 424_242);
        Assert.Equal(created.UserName, (await host.LoadAsync(created.UserId)).UserName);
    }
}
