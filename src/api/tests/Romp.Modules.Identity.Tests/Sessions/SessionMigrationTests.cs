using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Identity.Domain.Sessions;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Tests.Roles;

namespace Romp.Modules.Identity.Tests.Sessions;

/// <summary>Sessions task 4: the IDNT migrations create the session tables and seed their lookups; the entities round-trip through them.</summary>
public sealed class SessionMigrationTests(IdentityPostgres postgres) : IClassFixture<IdentityPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private async Task<long> NewUserAsync(string name)
    {
        await using var context = postgres.NewContext();
        var user = StaffUser.CreateWithTemporaryPassword(UserName.Create(name), "Test User", "hash-0", [], Now);
        context.Staff.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task Applies_AndSeedsLookups()
    {
        await using var context = postgres.NewContext();

        var tables = await context.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'IDNT'")
            .ToListAsync();
        Assert.Contains("SESS_MAIN", tables);
        Assert.Contains("SESS_RVKD_RSN_LKP", tables);
        Assert.Contains("LGIN_PEND", tables);
        Assert.Contains("LGIN_ATMP", tables);
        Assert.Contains("LGIN_OTCM_LKP", tables);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        // Every enum value has a lookup row with the same id and a stable, unique, non-empty code.
        var reasons = await context.Database
            .SqlQueryRaw<short>("SELECT \"ID\" AS \"Value\" FROM \"IDNT\".\"SESS_RVKD_RSN_LKP\"").ToListAsync();
        Assert.Equal(Enum.GetValues<SessionRevokeReason>().Select(v => (short)v).Order(), reasons.Order());

        var outcomes = await context.Database
            .SqlQueryRaw<short>("SELECT \"ID\" AS \"Value\" FROM \"IDNT\".\"LGIN_OTCM_LKP\"").ToListAsync();
        Assert.Equal(Enum.GetValues<LoginOutcome>().Select(v => (short)v).Order(), outcomes.Order());

        var reasonCodes = await context.Database
            .SqlQueryRaw<string>("SELECT \"CODE\" AS \"Value\" FROM \"IDNT\".\"SESS_RVKD_RSN_LKP\" WHERE \"ACT_IND\" AND \"NAME\" <> ''").ToListAsync();
        var outcomeCodes = await context.Database
            .SqlQueryRaw<string>("SELECT \"CODE\" AS \"Value\" FROM \"IDNT\".\"LGIN_OTCM_LKP\" WHERE \"ACT_IND\" AND \"NAME\" <> ''").ToListAsync();
        foreach (var codes in new[] { reasonCodes, outcomeCodes })
        {
            Assert.Equal(codes.Count, codes.Distinct().Count());
            Assert.All(codes, c => Assert.Matches("^[A-Z_]{2,30}$", c));
        }
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Session_RoundTrips_AndKeepsOnlyHashes()
    {
        var userId = await NewUserAsync("session.round.trip");
        var (session, tokens) = Session.Start(userId, Now, SessionLifetimes.Default, "203.0.113.9", new string('a', 500));
        var rotated = session.Rotate(tokens.Refresh.Reveal(), Now.AddMinutes(1));
        session.Revoke(SessionRevokeReason.SignOut, Now.AddMinutes(2));
        long id;
        await using (var context = postgres.NewContext())
        {
            context.Sessions.Add(session);
            await context.SaveChangesAsync();
            id = session.Id;
        }

        await using var read = postgres.NewContext();
        var loaded = await read.Sessions.SingleAsync(s => s.Id == id);

        Assert.Equal(userId, loaded.UserId);
        Assert.True(loaded.AccessTokenMatches(rotated.Tokens!.Access.Reveal()));
        Assert.Equal(session.RefreshTokenHash, loaded.RefreshTokenHash);
        Assert.NotNull(loaded.PreviousRefreshTokenHash);
        Assert.Equal(Now, loaded.StartedAt);
        Assert.Equal(Now.AddHours(12), loaded.AbsoluteExpiresAt);
        Assert.Equal(Now.AddMinutes(2), loaded.RevokedAt);
        Assert.Equal(SessionRevokeReason.SignOut, loaded.RevokeReason);
        Assert.Equal("203.0.113.9", loaded.ClientIp);
        Assert.Equal(Session.UserAgentMaxLength, loaded.UserAgent!.Length); // truncated, never rejected

        // What is stored is the 32-byte SHA-256 hash, not the 43-character value the browser holds.
        var storedLength = await read.Database
            .SqlQueryRaw<int>("SELECT octet_length(\"ACSS_TOKN_HASH\") AS \"Value\" FROM \"IDNT\".\"SESS_MAIN\" WHERE \"ID\" = {0}", id)
            .SingleAsync();
        Assert.Equal(32, storedLength);
        Assert.False(loaded.AccessTokenMatches(tokens.Access.Reveal())); // the pre-rotation value no longer matches
    }

    [Fact]
    [Trait("Spec", "AC-1")]
    public async Task Session_NeedsAUser_AndHashesAreUnique()
    {
        var userId = await NewUserAsync("session.constraints");
        var (good, _) = Session.Start(userId, Now, SessionLifetimes.Default);
        await using (var context = postgres.NewContext())
        {
            context.Sessions.Add(good);
            await context.SaveChangesAsync();
        }

        await using var orphanContext = postgres.NewContext();
        orphanContext.Sessions.Add(Session.Start(long.MaxValue, Now, SessionLifetimes.Default).Session);
        var orphan = await Assert.ThrowsAsync<DbUpdateException>(() => orphanContext.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)orphan.InnerException!).SqlState);

        // Two rows with the same refresh hash are impossible: lookup by hash must find exactly one session.
        await using var raw = postgres.NewContext();
        var duplicate = Session.Start(userId, Now, SessionLifetimes.Default).Session;
        raw.Sessions.Add(duplicate);
        await raw.SaveChangesAsync();
        var clash = await Assert.ThrowsAsync<PostgresException>(() => raw.Database.ExecuteSqlRawAsync(
            "UPDATE \"IDNT\".\"SESS_MAIN\" SET \"RFSH_TOKN_HASH\" = (SELECT \"RFSH_TOKN_HASH\" FROM \"IDNT\".\"SESS_MAIN\" WHERE \"ID\" = {0}) WHERE \"ID\" = {1}",
            good.Id, duplicate.Id));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, clash.SqlState);
    }

    [Fact]
    [Trait("Spec", "AC-8")]
    public async Task PendingSignIn_AndAttempts_RoundTrip_AndTheThrottleIndexExists()
    {
        var userId = await NewUserAsync("pending.attempts");
        var (pending, token) = PendingSignIn.Start(userId, Now);
        long pendingId;
        await using (var context = postgres.NewContext())
        {
            context.PendingSignIns.Add(pending);
            context.LoginAttempts.Add(LoginAttempt.Record("pending.attempts", "203.0.113.9", LoginOutcome.Ok, userId, Now));
            context.LoginAttempts.Add(LoginAttempt.Record("nobody", "203.0.113.9", LoginOutcome.BadCredentials, null, Now));
            await context.SaveChangesAsync();
            pendingId = pending.Id;
        }

        await using var read = postgres.NewContext();
        var loaded = await read.PendingSignIns.SingleAsync(p => p.Id == pendingId);
        Assert.Equal(Now.AddMinutes(5), loaded.ExpiresAt);
        Assert.Equal(0, loaded.FailureCount);
        Assert.True(SessionTokens.Matches(token.Reveal(), loaded.TokenHash));

        var attempts = await read.LoginAttempts.Where(a => a.AttemptName == "nobody").ToListAsync();
        var attempt = Assert.Single(attempts);
        Assert.Equal(LoginOutcome.BadCredentials, attempt.Outcome);
        Assert.Null(attempt.UserId); // unknown names are logged with no user

        var indexes = await read.Database
            .SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'IDNT' AND tablename = 'LGIN_ATMP'")
            .ToListAsync();
        Assert.Contains(indexes, d => d.Contains("(\"ATMP_NAME\", \"CLNT_IP_ADDR\", \"ATMP_DTE\")", StringComparison.Ordinal));
        Assert.Contains(indexes, d => d.Contains("(\"ATMP_NAME\", \"ATMP_DTE\")", StringComparison.Ordinal)); // the global per-name ceiling
    }
}
