using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Romp.Modules.Identity.Domain.AuditTrail;
using Romp.Modules.Identity.Infrastructure.AuditTrail;
using Romp.Modules.Identity.Tests.Roles;

namespace Romp.Modules.Identity.Tests.AuditTrail;

/// <summary>
/// The audit store itself (identity/audit-trail AC-4, task 5 and 6): append-only at the database level, partitioned by month, with the
/// partition function and its daily job.
/// </summary>
public sealed class AuditStoreTests(IdentityPostgres postgres) : IClassFixture<IdentityPostgres>
{
    private static long _messageSeed = 1_000;

    private static AuditEntry NewEntry(DateTimeOffset at, string key = "42", params string[] columns) =>
        AuditEntry.Create(at, "TEST", Interlocked.Increment(ref _messageSeed), "STYL_MAIN", key, AuditOperation.Update, "alice", "trace-1", columns.Length == 0 ? ["NAME"] : columns);

    private async Task<AuditEntry> StoreAsync(DateTimeOffset at)
    {
        await using var context = postgres.NewContext();
        await context.Database.ExecuteSqlAsync($"""SELECT "IDNT"."AUDT_PART_ENSR"({new DateOnly(at.Year, at.Month, 1)}, 1)""");
        var entry = NewEntry(at);
        context.AuditEntries.Add(entry);
        await context.SaveChangesAsync();
        return entry;
    }

    [Fact]
    [Trait("Spec", "FR-59")]
    [Trait("Spec", "AC-4")]
    public async Task UpdateAndDelete_AreRejectedByTheTrigger()
    {
        var entry = await StoreAsync(DateTimeOffset.UtcNow);
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();

        foreach (var sql in new[]
        {
            $"""UPDATE "IDNT"."AUDT_ENTY" SET "ACTR_NAME" = 'mallory' WHERE "ID" = {entry.Id}""",
            $"""DELETE FROM "IDNT"."AUDT_ENTY" WHERE "ID" = {entry.Id}""",
            $"""UPDATE "IDNT"."AUDT_CHNG" SET "COLM_NAME" = 'FORGED' WHERE "ENTY_ID" = {entry.Id}""",
            $"""DELETE FROM "IDNT"."AUDT_CHNG" WHERE "ENTY_ID" = {entry.Id}""",
            """TRUNCATE "IDNT"."AUDT_CHNG" """,
            """TRUNCATE "IDNT"."AUDT_ENTY" CASCADE""",
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Contains("append-only", error.MessageText, StringComparison.Ordinal);
        }

        // Nothing changed.
        await using var read = postgres.NewContext();
        var stored = await read.AuditEntries.AsNoTracking().Include(e => e.Columns).SingleAsync(e => e.Id == entry.Id);
        Assert.Equal("alice", stored.Actor);
        Assert.Equal(["NAME"], stored.Columns.Select(c => c.Name));
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task TruncatingAPartitionDirectly_IsRejectedToo()
    {
        var entry = await StoreAsync(DateTimeOffset.UtcNow);
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();
        var partition = $"AUDT_CHNG_{entry.OccurredAt:yyyyMM}";

        await using var command = new NpgsqlCommand($"""TRUNCATE "IDNT"."{partition}" """, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Contains("append-only", error.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Spec", "NFR-SC-05")]
    public async Task InsertsLandInTheRightPartition()
    {
        var march = new DateTimeOffset(2027, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var april = new DateTimeOffset(2027, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var marchEntry = await StoreAsync(march);
        var aprilEntry = await StoreAsync(april);

        await using var context = postgres.NewContext();
        var rows = await context.Database
            .SqlQuery<string>($"""SELECT tableoid::regclass::text AS "Value" FROM "IDNT"."AUDT_ENTY" WHERE "ID" IN ({marchEntry.Id}, {aprilEntry.Id}) ORDER BY "OCCR_DTE" """)
            .ToListAsync();

        Assert.Equal(["\"IDNT\".\"AUDT_ENTY_202703\"", "\"IDNT\".\"AUDT_ENTY_202704\""], rows);

        var columnRows = await context.Database
            .SqlQuery<string>($"""SELECT tableoid::regclass::text AS "Value" FROM "IDNT"."AUDT_CHNG" WHERE "ENTY_ID" IN ({marchEntry.Id}, {aprilEntry.Id}) ORDER BY "OCCR_DTE" """)
            .ToListAsync();
        Assert.Equal(["\"IDNT\".\"AUDT_CHNG_202703\"", "\"IDNT\".\"AUDT_CHNG_202704\""], columnRows);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task ABoundaryMoment_BelongsToTheNewMonth_InUtc()
    {
        var lastSecond = new DateTimeOffset(2027, 6, 30, 23, 59, 59, TimeSpan.Zero);
        var firstSecond = lastSecond.AddSeconds(1);
        var a = await StoreAsync(lastSecond);
        var b = await StoreAsync(firstSecond);

        await using var context = postgres.NewContext();
        var rows = await context.Database
            .SqlQuery<string>($"""SELECT tableoid::regclass::text AS "Value" FROM "IDNT"."AUDT_ENTY" WHERE "ID" IN ({a.Id}, {b.Id}) ORDER BY "OCCR_DTE" """)
            .ToListAsync();

        Assert.Equal(["\"IDNT\".\"AUDT_ENTY_202706\"", "\"IDNT\".\"AUDT_ENTY_202707\""], rows);
    }

    [Fact]
    [Trait("Spec", "AC-5")]
    public async Task TheSameMessageAndRecord_CannotBeStoredTwice()
    {
        var at = DateTimeOffset.UtcNow;
        var first = AuditEntry.Create(at, "TEST", 777_001, "STYL_MAIN", "9", AuditOperation.Insert, "alice", null, ["NAME"]);
        await using (var context = postgres.NewContext())
        {
            await context.Database.ExecuteSqlAsync($"""SELECT "IDNT"."AUDT_PART_ENSR"({new DateOnly(at.Year, at.Month, 1)}, 1)""");
            context.AuditEntries.Add(first);
            await context.SaveChangesAsync();
        }

        await using var again = postgres.NewContext();
        again.AuditEntries.Add(AuditEntry.Create(at, "TEST", 777_001, "STYL_MAIN", "9", AuditOperation.Insert, "alice", null, ["NAME"]));
        await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync());
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task PartitionFunction_IsIdempotent_AndCreatesFutureMonths()
    {
        await using var context = postgres.NewContext();
        var first = new DateOnly(2030, 1, 1);

        await context.Database.ExecuteSqlAsync($"""SELECT "IDNT"."AUDT_PART_ENSR"({first}, 3)""");
        await context.Database.ExecuteSqlAsync($"""SELECT "IDNT"."AUDT_PART_ENSR"({first}, 3)""");
        await context.Database.ExecuteSqlAsync($"""SELECT "IDNT"."AUDT_PART_ENSR"({first.AddMonths(2)}, 2)""");

        var names = await context.Database
            .SqlQuery<string>($"""SELECT c.relname AS "Value" FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'IDNT' AND c.relkind = 'r' AND c.relname LIKE 'AUDT\_%\_2030%' ORDER BY c.relname""")
            .ToListAsync();

        Assert.Equal(
            ["AUDT_CHNG_203001", "AUDT_CHNG_203002", "AUDT_CHNG_203003", "AUDT_CHNG_203004", "AUDT_ENTY_203001", "AUDT_ENTY_203002", "AUDT_ENTY_203003", "AUDT_ENTY_203004"],
            names);
    }

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task PartitionJob_CreatesTheNextMonths_Idempotently()
    {
        var services = new ServiceCollection();
        services.AddDbContext<Infrastructure.Persistence.IdntDbContext>(o => o.UseNpgsql(postgres.ConnectionString, n => n.MigrationsHistoryTable("MIG_HIST", "IDNT")));
        await using var provider = services.BuildServiceProvider();
        var now = new DateTimeOffset(2031, 11, 20, 8, 0, 0, TimeSpan.Zero);
        var worker = new AuditPartitionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new Support.FixedTimeProvider(now),
            NullLogger<AuditPartitionWorker>.Instance);

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        await using var context = postgres.NewContext();
        var months = await context.Database
            .SqlQuery<string>($"""SELECT c.relname AS "Value" FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'IDNT' AND c.relkind = 'r' AND (c.relname LIKE 'AUDT\_ENTY\_2031%' OR c.relname LIKE 'AUDT\_ENTY\_2032%') ORDER BY c.relname""")
            .ToListAsync();
        Assert.Equal(["AUDT_ENTY_203111", "AUDT_ENTY_203112", "AUDT_ENTY_203201", "AUDT_ENTY_203202"], months);
    }

    [Fact]
    [Trait("Spec", "AC-6")]
    public void AnEntry_NeverCarriesAForgedActorOrColumn()
    {
        var at = DateTimeOffset.UtcNow;

        var forged = AuditEntry.Create(at, "TEST", 1, "STYL_MAIN", "1", AuditOperation.Update, "bob\nINFO forged line", "x\ny", ["NAME"]);
        Assert.Equal(AuditEntry.UnknownActor, forged.Actor);
        Assert.Null(forged.CorrelationId);

        Assert.Throws<ArgumentException>(() => AuditEntry.Create(at, "TEST", 1, "STYL_MAIN", "1", AuditOperation.Update, "bob", null, ["name; DROP TABLE"]));
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(at, "TEST", 1, "STYL_MAIN", "1\n2", AuditOperation.Update, "bob", null, ["NAME"]));
        Assert.Throws<ArgumentOutOfRangeException>(() => AuditEntry.Create(at, "TEST", 1, "STYL_MAIN", "1", (AuditOperation)9, "bob", null, ["NAME"]));
    }
}
