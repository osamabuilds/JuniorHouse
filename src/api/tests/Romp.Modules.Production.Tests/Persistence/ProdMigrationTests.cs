using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Production.Domain;
using Romp.Modules.Production.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Production.Tests.Persistence;

/// <summary>
/// SCRUM-187..191, spec AC-2 / AC-20: the PROD migration against a real PostgreSQL (Testcontainers,
/// CLAUDE.md) - lookups seeded with stable ids, the schema and constraints as designed, and the EF model matching the migrations.
/// </summary>
public sealed class ProdMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private ProductionDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ProductionDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.MigrationsHistoryTable("MIG_HIST", "PROD"))
            .Options);

    [Fact]
    [Trait("Spec", "AC-4")]
    public async Task Migrate_Lookups_SeededWithStableCodes()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();

        Assert.Equal(
            ["FabricBooked", "FabricDispatchedByRomp", "PPSampleSubmitted", "PPSampleApproved", "PPSampleRejected", "BulkCuttingStarted", "FinishedReadyToShip"],
            await context.Set<MilestoneTypeLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(["Open", "Cancelled", "Closed"], await context.Set<RunStatusLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(["Pending", "Approved", "Rejected"], await context.Set<PpDecisionLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(["OnTime", "Late", "BeyondAcceptable"], await context.Set<DeliveryTimingLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
        Assert.Equal(["OnQuantity", "Short", "Over"], await context.Set<DeliveryQuantityStatusLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync());
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    [Trait("Spec", "AC-20")]
    public async Task Migrate_Schema_HasTheTablesUniquesAndIndexedForeignKeys()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        var tables = await ScalarListAsync(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'PROD' ORDER BY table_name");
        foreach (var expected in new[]
        {
            "PROD_RUN_MAIN", "PROD_MLST", "PROD_MLST_FNSH_LINE", "PROD_EXPC_HIST", "PP_SMPL_RND", "PP_SMPL_FILE", "DLVR_NOTE", "DLVR_NOTE_LINE",
            "INBX", "OUTB_MSG", "PROD_RUN_STS_LKP", "MLST_TYP_LKP", "PP_SMPL_DCSN_LKP", "DLVR_TMNG_LKP", "DLVR_QTY_STS_LKP",
        })
        {
            Assert.Contains(expected, tables);
        }

        // AC-2 / AC-20: uniqueness the domain relies on is also enforced by the database.
        var uniques = await ScalarListAsync(connection,
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'PROD' AND indexdef LIKE 'CREATE UNIQUE%'");
        Assert.Contains(uniques, u => u.Contains("\"PROD_RUN_MAIN\"") && u.Contains("(\"PO_ID\")"));
        Assert.Contains(uniques, u => u.Contains("\"DLVR_NOTE\"") && u.Contains("(\"RUN_ID\", \"VNDR_DN_NO\")"));
        Assert.Contains(uniques, u => u.Contains("\"PP_SMPL_RND\"") && u.Contains("(\"RUN_ID\", \"RND_NO\")"));

        // Every declared foreign key column is covered by an index (docs/db/naming.md rule 3).
        var unindexed = await ScalarListAsync(connection, """
            SELECT c.conrelid::regclass::text || '.' || a.attname
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
            WHERE c.contype = 'f' AND c.connamespace = '"PROD"'::regnamespace
              AND NOT EXISTS (
                SELECT 1 FROM pg_index i
                WHERE i.indrelid = c.conrelid AND i.indkey[0] = c.conkey[1])
            """);
        Assert.Empty(unindexed);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Migrate_DuplicateRunForOnePo_RejectedByTheDatabase()
    {
        await using var context = NewContext();
        await context.Database.MigrateAsync();
        var runA = new Domain.ProductionRuns.ProductionRun(9001, "PO-2026-09001", 1, new DateOnly(2026, 9, 1), null);
        var runB = new Domain.ProductionRuns.ProductionRun(9001, "PO-2026-09001", 1, new DateOnly(2026, 9, 1), null);
        context.Runs.Add(runA);
        await context.SaveChangesAsync();
        await using var second = NewContext();
        second.Runs.Add(runB);

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public void Model_HasNoPendingChanges()
    {
        using var context = NewContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static async Task<List<string>> ScalarListAsync(NpgsqlConnection connection, string sql)
    {
        var result = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }
}
