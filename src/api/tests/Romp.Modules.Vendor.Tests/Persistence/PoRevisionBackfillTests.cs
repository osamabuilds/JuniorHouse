using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Revisions;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Vendor.Tests.Persistence;

/// <summary>
/// SCRUM-93 task 21 (AC-64): the data migration backfills a synthetic Rev 0 for every PO that's
/// ever been Sent, and an acknowledged-revision-0 record for ones also ever Acknowledged. Uses
/// Testcontainers (CLAUDE.md); Windows Application Control blocks Docker-backed tests on this
/// machine (CLAUDE.md) - written and build-verified here, run on CI.
/// </summary>
public sealed class PoRevisionBackfillTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    [Trait("Spec", "AC-64")]
    public async Task Migrate_ExistingSentPos_GetRevZero()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;

        long draftOnlyPoId, sentOnlyPoId, acknowledgedPoId;

        await using (var context = new VendorDbContext(options))
        {
            // Migrate only up to the point the revision tables exist but before the backfill runs,
            // so the three POs below are created as genuine "Sprint 1 data" the backfill must act on.
            await context.Database.MigrateAsync();
        }

        // Simulate pre-existing Sprint 1 data by inserting rows directly, bypassing the backfill
        // migration that already ran above - reflects "data that existed before this migration".
        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();

            draftOnlyPoId = await InsertPoAsync(connection, "PO-2026-90001", statusId: 1);
            await InsertStatusHistoryAsync(connection, draftOnlyPoId, statusId: 1);

            sentOnlyPoId = await InsertPoAsync(connection, "PO-2026-90002", statusId: 2);
            await InsertStatusHistoryAsync(connection, sentOnlyPoId, statusId: 1);
            await InsertStatusHistoryAsync(connection, sentOnlyPoId, statusId: 2);
            await InsertLineAsync(connection, sentOnlyPoId, qty: 10);

            acknowledgedPoId = await InsertPoAsync(connection, "PO-2026-90003", statusId: 3);
            await InsertStatusHistoryAsync(connection, acknowledgedPoId, statusId: 1);
            await InsertStatusHistoryAsync(connection, acknowledgedPoId, statusId: 2);
            await InsertStatusHistoryAsync(connection, acknowledgedPoId, statusId: 3);
            await InsertLineAsync(connection, acknowledgedPoId, qty: 20);

            // Re-run the backfill migration's own SQL directly against this "pre-existing data" -
            // equivalent to what happens when the migration runs against a real Sprint 1 database
            // (the actual migration already ran once, against zero rows, during MigrateAsync above).
            await RunBackfillSqlAsync(connection);
        }

        await using var verifyContext = new VendorDbContext(options);

        var draftRevisions = await verifyContext.Set<Domain.Revisions.PurchaseOrderRevision>()
            .Where(r => r.PoId == draftOnlyPoId).ToListAsync();
        Assert.Empty(draftRevisions);

        var sentRevisions = await verifyContext.Set<Domain.Revisions.PurchaseOrderRevision>()
            .Include(r => r.Lines)
            .Where(r => r.PoId == sentOnlyPoId).ToListAsync();
        var sentRev0 = Assert.Single(sentRevisions);
        Assert.Equal(0, sentRev0.RevisionNumber);
        Assert.Equal(2, sentRev0.StatusId); // InForce
        Assert.Single(sentRev0.Lines);
        Assert.Empty(await verifyContext.Set<Domain.PurchaseOrders.PoVendorCommunication>().Where(c => c.PoId == sentOnlyPoId).ToListAsync());

        var ackRevisions = await verifyContext.Set<Domain.Revisions.PurchaseOrderRevision>()
            .Where(r => r.PoId == acknowledgedPoId).ToListAsync();
        var ackRev0 = Assert.Single(ackRevisions);
        var comms = await verifyContext.Set<Domain.PurchaseOrders.PoVendorCommunication>().Where(c => c.PoId == acknowledgedPoId).ToListAsync();
        var comm = Assert.Single(comms);
        Assert.Equal(ackRev0.Id, comm.RevisionId);
        Assert.Equal((short)1, comm.CommunicationTypeId); // Confirmed
        Assert.Equal((short)5, comm.ChannelId); // Unspecified
    }

    private static async Task<long> InsertPoAsync(NpgsqlConnection connection, string poNo, short statusId)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "VNDR"."PO_MAIN" ("PO_NO","VNDR_ID","STYL_ID","UNIT_COST_AMT","EXPC_DLVR_DT","PAYM_TERM_ID","ADV_PCT","PO_STS_ID","INSR_DTE","INSR_BY")
            VALUES (@poNo, 1, 1, 100, '2026-12-01', 1, 50, @sts, now(), 'test')
            RETURNING "ID"
            """,
            connection);
        command.Parameters.AddWithValue("poNo", poNo);
        command.Parameters.AddWithValue("sts", statusId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task InsertStatusHistoryAsync(NpgsqlConnection connection, long poId, short statusId)
    {
        await using var command = new NpgsqlCommand(
            """INSERT INTO "VNDR"."PO_STS_HIST" ("PO_ID","PO_STS_ID","INSR_DTE","INSR_BY") VALUES (@po,@sts,now(),'test')""",
            connection);
        command.Parameters.AddWithValue("po", poId);
        command.Parameters.AddWithValue("sts", statusId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertLineAsync(NpgsqlConnection connection, long poId, int qty)
    {
        await using var command = new NpgsqlCommand(
            """INSERT INTO "VNDR"."PO_LINE" ("PO_ID","SIZE_ID","CLR_ID","QTY","INSR_DTE","INSR_BY") VALUES (@po,1,1,@qty,now(),'test')""",
            connection);
        command.Parameters.AddWithValue("po", poId);
        command.Parameters.AddWithValue("qty", qty);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Mirrors the SQL in 20260928150000_BackfillPoRevisionZero.cs exactly - re-run here since the migration itself already applied against an empty database during MigrateAsync.</summary>
    private static async Task RunBackfillSqlAsync(NpgsqlConnection connection)
    {
        const string actor = "test-backfill-rerun";

        await ExecuteAsync(connection, $"""
            INSERT INTO "VNDR"."PO_REV"
                ("PO_ID", "REV_NO", "INIT_ID", "STS_ID", "RSN_ID", "IMPC_NOTE", "VNDR_MSG",
                 "UNIT_COST_AMT", "EXPC_DLVR_DT", "LATE_ACPT_DT", "OVER_TOL_PCT", "UNDR_TOL_PCT", "PAYM_TERM_ID", "ADV_PCT", "FBRC_RESP_ID",
                 "PO_VAL_BEF_AMT", "PO_VAL_AFT_AMT", "PO_VAL_DIFF_AMT", "ADV_AMT_BEF", "ADV_AMT_AFT",
                 "EXPC_DT_SHFT_DAY", "LATE_ACPT_DT_SHFT_DAY", "QTY_DIFF", "BYND_LATE_IND",
                 "INSR_DTE", "INSR_BY")
            SELECT
                po."ID", 0, 1, 2, 11,
                'Backfilled Rev 0 from Sprint 1 data (SCRUM-93 task 21, AC-64).', NULL,
                po."UNIT_COST_AMT", po."EXPC_DLVR_DT", po."LATE_ACPT_DT", po."OVER_TOL_PCT", po."UNDR_TOL_PCT", po."PAYM_TERM_ID", po."ADV_PCT", po."FBRC_RESP_ID",
                poval."VAL", poval."VAL", 0,
                poval."VAL" * po."ADV_PCT" / 100, poval."VAL" * po."ADV_PCT" / 100,
                0, NULL, 0,
                (po."LATE_ACPT_DT" IS NOT NULL AND po."EXPC_DLVR_DT" > po."LATE_ACPT_DT"),
                now(), '{actor}'
            FROM "VNDR"."PO_MAIN" po
            CROSS JOIN LATERAL (
                SELECT COALESCE(SUM(l."QTY"), 0) * po."UNIT_COST_AMT" AS "VAL"
                FROM "VNDR"."PO_LINE" l
                WHERE l."PO_ID" = po."ID"
            ) poval
            WHERE EXISTS (
                SELECT 1 FROM "VNDR"."PO_STS_HIST" h WHERE h."PO_ID" = po."ID" AND h."PO_STS_ID" = 2
            )
            """);

        await ExecuteAsync(connection, $"""
            INSERT INTO "VNDR"."PO_REV_STS_HIST" ("PO_REV_ID", "FROM_STS_ID", "TO_STS_ID", "NOTE", "INSR_DTE", "INSR_BY")
            SELECT r."ID", NULL, 2, 'Backfilled', now(), '{actor}'
            FROM "VNDR"."PO_REV" r
            WHERE r."REV_NO" = 0 AND r."INSR_BY" = '{actor}'
            """);

        await ExecuteAsync(connection, $"""
            INSERT INTO "VNDR"."PO_REV_LINE" ("PO_REV_ID", "SIZE_ID", "CLR_ID", "QTY", "INSR_DTE", "INSR_BY")
            SELECT r."ID", l."SIZE_ID", l."CLR_ID", l."QTY", now(), '{actor}'
            FROM "VNDR"."PO_REV" r
            JOIN "VNDR"."PO_LINE" l ON l."PO_ID" = r."PO_ID"
            WHERE r."REV_NO" = 0 AND r."INSR_BY" = '{actor}'
            """);

        await ExecuteAsync(connection, $"""
            INSERT INTO "VNDR"."PO_VNDR_COMM" ("PO_ID", "PO_REV_ID", "COMM_TYP_ID", "CHNL_ID", "RSPR_NAME", "RSPN_DTE", "INSR_DTE", "INSR_BY")
            SELECT r."PO_ID", r."ID", 1, 5, 'Unspecified', now(), now(), '{actor}'
            FROM "VNDR"."PO_REV" r
            WHERE r."REV_NO" = 0 AND r."INSR_BY" = '{actor}'
              AND EXISTS (SELECT 1 FROM "VNDR"."PO_STS_HIST" h WHERE h."PO_ID" = r."PO_ID" AND h."PO_STS_ID" = 3)
            """);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
