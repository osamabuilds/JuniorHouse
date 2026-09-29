using Microsoft.EntityFrameworkCore;
using Npgsql;
using Romp.Modules.Vendor.Domain.PurchaseOrders;
using Romp.Modules.Vendor.Domain.Vendors;
using Romp.Modules.Vendor.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Romp.Modules.Vendor.Tests.Persistence;

/// <summary>
/// SCRUM-93 task 19: migrating a real PostgreSQL database creates the 5 new revision/file/
/// communication tables, and the partial unique index enforces at most one Pending revision per PO
/// at the DB level (ADR 0007). Task 20+ adds the domain entity's own creation behaviour - this test
/// writes rows via raw SQL since the entities are still constructor-internal skeletons (task 19's
/// scope is schema-only). Uses Testcontainers (CLAUDE.md); Windows Application Control blocks
/// Docker-backed tests on this machine (CLAUDE.md) - written and build-verified here, run on CI.
/// </summary>
public sealed class PoRevisionMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migrate_CreatesRevisionTablesWithPartialUniqueIndex()
    {
        var options = new DbContextOptionsBuilder<VendorDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        long poId;

        await using (var context = new VendorDbContext(options))
        {
            await context.Database.MigrateAsync();

            var po = new Domain.PurchaseOrders.PurchaseOrder(
                "PO-2026-00010", vendorId: 1, styleId: 1, unitCost: 100m,
                expectedDeliveryDate: new DateOnly(2026, 12, 1), paymentTermId: 1, advancePercent: 50m,
                lines: [(1, 1, 10)]);
            context.PurchaseOrders.Add(po);
            await context.SaveChangesAsync();
            poId = po.Id;
        }

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        // Rev 1 (InForce) and Rev 2 (Pending) both insert cleanly - only one Pending is the limit.
        await InsertRevisionAsync(connection, poId, revNo: 1, statusId: RevisionStatusInForce);
        var pendingRevId = await InsertRevisionAsync(connection, poId, revNo: 2, statusId: RevisionStatusPending);

        await ExecuteAsync(
            connection,
            """INSERT INTO "VNDR"."PO_REV_LINE" ("PO_REV_ID","SIZE_ID","CLR_ID","QTY","INSR_DTE","INSR_BY") VALUES (@rev,1,1,10,now(),'test')""",
            ("rev", pendingRevId));

        await ExecuteAsync(
            connection,
            """INSERT INTO "VNDR"."PO_REV_STS_HIST" ("PO_REV_ID","TO_STS_ID","INSR_DTE","INSR_BY") VALUES (@rev,1,now(),'test')""",
            ("rev", pendingRevId));

        await using var commCmd = new NpgsqlCommand(
            """
            INSERT INTO "VNDR"."PO_VNDR_COMM" ("PO_ID","COMM_TYP_ID","CHNL_ID","RSPR_NAME","RSPN_DTE","INSR_DTE","INSR_BY")
            VALUES (@po,1,1,'Vendor Rep',now(),now(),'test')
            RETURNING "ID"
            """,
            connection);
        commCmd.Parameters.AddWithValue("po", poId);
        var vendorCommId = (long)(await commCmd.ExecuteScalarAsync())!;

        await ExecuteAsync(
            connection,
            """
            INSERT INTO "VNDR"."PO_FILE" ("PO_ID","CATG_ID","FILE_NAME","STOR_KEY","CNTT_TYP","FILE_SIZE_BYT","VNDR_COMM_ID","INSR_DTE","INSR_BY")
            VALUES (@po,1,'a.pdf','generated-key-1','application/pdf',100,@comm,now(),'test')
            """,
            ("po", poId),
            ("comm", vendorCommId));

        // ADR 0007: at most one Pending revision per PO - the second Pending insert must be rejected
        // by the DB's own partial unique index (IX_PO_REV_PO_ID_PEND), not just application logic.
        await Assert.ThrowsAsync<PostgresException>(
            () => InsertRevisionAsync(connection, poId, revNo: 3, statusId: RevisionStatusPending));
    }

    private const short RevisionStatusInForce = 2;
    private const short RevisionStatusPending = 1;

    private static async Task<long> InsertRevisionAsync(NpgsqlConnection connection, long poId, short revNo, short statusId)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "VNDR"."PO_REV"
                ("PO_ID","REV_NO","INIT_ID","STS_ID","RSN_ID","IMPC_NOTE","UNIT_COST_AMT","EXPC_DLVR_DT","PAYM_TERM_ID","ADV_PCT",
                 "PO_VAL_BEF_AMT","PO_VAL_AFT_AMT","PO_VAL_DIFF_AMT","ADV_AMT_BEF","ADV_AMT_AFT","EXPC_DT_SHFT_DAY","QTY_DIFF","BYND_LATE_IND",
                 "INSR_DTE","INSR_BY")
            VALUES (@po,@revNo,1,@sts,1,'note',100,'2026-12-01',1,50,1000,1000,0,500,500,0,0,false,now(),'test')
            RETURNING "ID"
            """,
            connection);
        command.Parameters.AddWithValue("po", poId);
        command.Parameters.AddWithValue("revNo", revNo);
        command.Parameters.AddWithValue("sts", statusId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
