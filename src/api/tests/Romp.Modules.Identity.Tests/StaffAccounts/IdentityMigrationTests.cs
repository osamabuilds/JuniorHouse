using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Identity.Domain.StaffAccounts;
using Romp.Modules.Identity.Tests.Roles;

namespace Romp.Modules.Identity.Tests.StaffAccounts;

/// <summary>Staff-accounts step 4: the IDNT migrations apply to a clean PostgreSQL and the aggregate round-trips through them.</summary>
public sealed class IdentityMigrationTests(IdentityPostgres postgres) : IClassFixture<IdentityPostgres>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private static StaffUser NewUser(string name, params short[] roles) =>
        StaffUser.CreateWithTemporaryPassword(UserName.Create(name), "Test User", "hash-0", roles, Now);

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Applies_OnACleanDatabase()
    {
        await using var context = postgres.NewContext();

        var tables = await context.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'IDNT'")
            .ToListAsync();

        Assert.Contains("USR_MAIN", tables);
        Assert.Contains("USR_PWD_HIST", tables);
        Assert.Contains("USR_ROLE_MAP", tables);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task Account_RoundTrips_WithRolesAndHistory()
    {
        long id;
        await using (var context = postgres.NewContext())
        {
            var user = NewUser("round.trip", 4, 5);
            user.SetPassword("hash-1", _ => false);
            user.SetPassword("hash-2", _ => false);
            context.Staff.Add(user);
            await context.SaveChangesAsync();
            id = user.Id;
        }

        await using var read = postgres.NewContext();
        var loaded = await read.Staff.Include(u => u.Roles).Include(u => u.PasswordHistory).SingleAsync(u => u.Id == id);

        Assert.Equal("round.trip", loaded.UserName);
        Assert.Equal("hash-2", loaded.PasswordHash);
        Assert.True(loaded.IsActive);
        Assert.False(loaded.MustChangePassword);
        Assert.Null(loaded.TemporaryPasswordExpiresAt);
        Assert.Equal([(short)4, (short)5], loaded.RoleIds.Order());
        Assert.Equal(["hash-0", "hash-1"], loaded.PasswordHistory.Select(h => h.PasswordHash).Order());
    }

    [Fact]
    [Trait("Spec", "AC-10")]
    public async Task UserName_IsUniqueAndLowerCaseOnly()
    {
        await using (var context = postgres.NewContext())
        {
            context.Staff.Add(NewUser("unique.name"));
            await context.SaveChangesAsync();
        }

        await using var duplicate = postgres.NewContext();
        duplicate.Staff.Add(NewUser("unique.name"));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)ex.InnerException!).SqlState);

        await using var raw = postgres.NewContext();
        var bad = await Assert.ThrowsAsync<PostgresException>(() => raw.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"IDNT\".\"USR_MAIN\" (\"USR_NAME\", \"DISP_NAME\", \"PWD_HASH\", \"ACT_IND\", \"MUST_CHNG_PWD_IND\", \"INSR_DTE\", \"INSR_BY\") " +
            "VALUES ('Upper.Case', 'x', 'h', true, true, now(), 'test')"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, bad.SqlState);
    }

    [Fact]
    [Trait("Spec", "AC-3")]
    public async Task Role_MustExist()
    {
        await using var context = postgres.NewContext();
        context.Staff.Add(NewUser("bad.role", 999));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)ex.InnerException!).SqlState);
    }

    [Fact]
    [Trait("Spec", "AC-9")]
    public async Task ConcurrentEdits_AreDetectedByTheRowVersion()
    {
        long id;
        await using (var setup = postgres.NewContext())
        {
            var user = NewUser("concurrent.edit");
            setup.Staff.Add(user);
            await setup.SaveChangesAsync();
            id = user.Id;
        }

        await using var first = postgres.NewContext();
        await using var second = postgres.NewContext();
        var a = await first.Staff.SingleAsync(u => u.Id == id);
        var b = await second.Staff.SingleAsync(u => u.Id == id);
        a.Reactivate();
        a.IssueTemporaryPassword("hash-a", Now);
        b.IssueTemporaryPassword("hash-b", Now);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
