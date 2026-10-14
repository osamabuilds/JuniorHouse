using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Quality.Domain;
using Romp.Modules.Quality.Tests.Support;

namespace Romp.Modules.Quality.Tests.Persistence;

/// <summary>
/// Spec AC-2 / AC-4 / AC-13 / AC-22: the QC migrations against a real PostgreSQL (Testcontainers, CLAUDE.md), with
/// lookups seeded under stable ids, the schema and constraints as designed, and the EF model matching the migrations.
/// </summary>
public sealed class QcMigrationTests : IAsyncLifetime
{
    private QcPostgres _postgres = null!;

    public async Task InitializeAsync() => _postgres = await QcPostgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    [Trait("Spec", "AC-13")]
    [Trait("Spec", "AC-20")]
    public async Task Migrate_Lookups_SeededWithStableCodes()
    {
        await using var context = _postgres.NewContext();

        Assert.Equal(["Recorded"], await Codes<GrnStatusLookup>(context));
        Assert.Equal(["Matched", "Short", "Over"], await Codes<GrnReconciliationLookup>(context));
        Assert.Equal(["Pending", "InInspection", "Decided", "AwaitingRework", "Closed"], await Codes<BatchStatusLookup>(context));
        Assert.Equal(["Accepted", "Concession", "Rejected", "Rework"], await Codes<OutcomeLookup>(context));
        Assert.Equal(["Aql", "Full"], await Codes<InspectionModeLookup>(context));
        Assert.Equal(["Pass", "Fail"], await Codes<InspectionResultLookup>(context));
        Assert.Equal(["S1", "S2", "S3", "S4", "I", "II", "III"], await Codes<InspectionLevelLookup>(context));
        Assert.Equal(["Aql0_65", "Aql1_0", "Aql1_5", "Aql2_5", "Aql4_0", "Aql6_5"], await Codes<AqlLevelLookup>(context));
        Assert.Equal(["Stitching", "Measurements", "FabricColourMatch", "TrimsSecure", "VisibleDefects", "CareLabel"], await Codes<CheckTypeLookup>(context));
        Assert.Equal(["Pass", "Fail", "NotApplicable"], await Codes<CheckResultLookup>(context));
        Assert.Equal(["ChokingHazardTrims", "DrawstringNeckline"], await Codes<SafetyTypeLookup>(context));
        Assert.Equal(["Stitching", "Measurement", "FabricColour", "TrimsSecurity", "VisibleDefect", "CareLabel", "Other"], await Codes<DefectTypeLookup>(context));
        Assert.Equal(["Critical", "Major", "Minor"], await Codes<DefectSeverityLookup>(context));
        Assert.Equal(2.5m, await context.Set<AqlLevelLookup>().Where(l => l.Id == AqlLevel.Aql250).Select(l => l.AqlValue).SingleAsync());
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    [Trait("Spec", "AC-4")]
    [Trait("Spec", "AC-22")]
    public async Task Migrate_Schema_HasTheTablesUniquesAndIndexedForeignKeys()
    {
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();

        var tables = await ScalarListAsync(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'QC' ORDER BY table_name");
        foreach (var expected in new[]
        {
            "INBD_DLVR", "INBD_DLVR_LINE", "GRN_MAIN", "GRN_LINE", "GRN_NO_SEQ", "QC_BTCH_MAIN", "QC_BTCH_LINE", "QC_BTCH_SPLT",
            "QC_BTCH_SPLT_LINE", "QC_INSP_MAIN", "QC_INSP_CHK", "QC_INSP_SAFE", "QC_INSP_SAFE_LINE", "QC_DFCT", "QC_RWRK_RTRN",
            "QC_AQL_SMPL", "QC_AQL_LIMT", "INBX", "OUTB_MSG", "GRN_STS_LKP", "GRN_RCNL_LKP", "QC_BTCH_STS_LKP", "QC_OTCM_LKP",
            "QC_INSP_MODE_LKP", "QC_INSP_RSLT_LKP", "QC_INSP_LVL_LKP", "QC_AQL_LVL_LKP", "QC_CHK_TYP_LKP", "QC_CHK_RSLT_LKP",
            "QC_SAFE_TYP_LKP", "QC_DFCT_TYP_LKP", "QC_DFCT_SVRT_LKP",
        })
        {
            Assert.Contains(expected, tables);
        }

        var uniques = await ScalarListAsync(connection,
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'QC' AND indexdef LIKE 'CREATE UNIQUE%'");
        Assert.Contains(uniques, u => u.Contains("\"INBD_DLVR\"") && u.Contains("(\"RUN_ID\", \"VNDR_DN_NO\")"));
        Assert.Contains(uniques, u => u.Contains("\"GRN_MAIN\"") && u.Contains("(\"INBD_DLVR_ID\")"));
        Assert.Contains(uniques, u => u.Contains("\"GRN_MAIN\"") && u.Contains("(\"GRN_NO\")"));
        Assert.Contains(uniques, u => u.Contains("\"QC_BTCH_MAIN\"") && u.Contains("(\"GRN_ID\")"));
        Assert.Contains(uniques, u => u.Contains("\"QC_BTCH_SPLT\"") && u.Contains("(\"BTCH_ID\", \"SPLT_NO\")"));

        // Every declared foreign key column is covered by an index (docs/db/naming.md rule 3).
        var unindexed = await ScalarListAsync(connection, """
            SELECT c.conrelid::regclass::text || '.' || a.attname
            FROM pg_constraint c
            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
            WHERE c.contype = 'f' AND c.connamespace = '"QC"'::regnamespace
              AND NOT EXISTS (
                SELECT 1 FROM pg_index i
                WHERE i.indrelid = c.conrelid AND i.indkey[0] = c.conkey[1])
            """);
        Assert.Empty(unindexed);
    }

    [Fact]
    [Trait("Spec", "AC-2")]
    public async Task Migrate_TwoInboundDeliveriesWithTheSameRunAndDnNo_RejectedByTheDatabase()
    {
        await using var first = _postgres.NewContext();
        first.Deliveries.Add(NewDelivery());
        await first.SaveChangesAsync();
        await using var second = _postgres.NewContext();
        second.Deliveries.Add(NewDelivery());

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public void Model_HasNoPendingChanges()
    {
        using var context = _postgres.NewContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static Romp.Modules.Quality.Domain.InboundDeliveries.InboundDelivery NewDelivery() =>
        new(runId: 1, poId: 10, poNo: "PO-2026-00010", vendorDnNo: "DN-1", new DateOnly(2026, 9, 20), false, [new(1, 10, 100)]);

    private static async Task<List<string>> Codes<TLookup>(Romp.Modules.Quality.Infrastructure.Persistence.QcDbContext context)
        where TLookup : QualityLookup =>
        await context.Set<TLookup>().OrderBy(l => l.Id).Select(l => l.Code).ToListAsync();

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
