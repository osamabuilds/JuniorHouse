using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

namespace Romp.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PAYM_RVW_LKP",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYM_RVW_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PAYM_STS_LKP",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYM_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "WBHK_OTCM_LKP",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WBHK_OTCM_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PAYM_MAIN",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    PAYM_METH_ID = table.Column<short>(type: "smallint", nullable: false),
                    PAYM_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    WNDW_END_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PAID_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RVW_RSN_ID = table.Column<short>(type: "smallint", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYM_MAIN", x => x.ID);
                    table.CheckConstraint("CK_PAYM_MAIN_AMT", "\"AMT\" > 0");
                    table.ForeignKey(
                        name: "FK_PAYM_MAIN_ORDR_MAIN",
                        column: x => x.ORDR_ID,
                        principalSchema: "ORDR",
                        principalTable: "ORDR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYM_MAIN_PAYM_METH_LKP",
                        column: x => x.PAYM_METH_ID,
                        principalSchema: "ORDR",
                        principalTable: "PAYM_METH_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYM_MAIN_PAYM_RVW_LKP",
                        column: x => x.RVW_RSN_ID,
                        principalSchema: "ORDR",
                        principalTable: "PAYM_RVW_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYM_MAIN_PAYM_STS_LKP",
                        column: x => x.PAYM_STS_ID,
                        principalSchema: "ORDR",
                        principalTable: "PAYM_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAYM_ATMP",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PAYM_ID = table.Column<long>(type: "bigint", nullable: false),
                    ATMP_NO = table.Column<int>(type: "integer", nullable: false),
                    PRVD_REF = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    URL = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PAYM_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    STRT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    END_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYM_ATMP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PAYM_ATMP_PAYM_MAIN",
                        column: x => x.PAYM_ID,
                        principalSchema: "ORDR",
                        principalTable: "PAYM_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYM_ATMP_PAYM_STS_LKP",
                        column: x => x.PAYM_STS_ID,
                        principalSchema: "ORDR",
                        principalTable: "PAYM_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PAYM_WBHK",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRVD = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    EVNT_ID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SGNT_VRFD_IND = table.Column<bool>(type: "boolean", nullable: false),
                    HASH = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WBHK_OTCM_ID = table.Column<short>(type: "smallint", nullable: false),
                    PRVD_REF = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PAYM_ID = table.Column<long>(type: "bigint", nullable: true),
                    RCVD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYM_WBHK", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PAYM_WBHK_PAYM_MAIN",
                        column: x => x.PAYM_ID,
                        principalSchema: "ORDR",
                        principalTable: "PAYM_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAYM_WBHK_WBHK_OTCM_LKP",
                        column: x => x.WBHK_OTCM_ID,
                        principalSchema: "ORDR",
                        principalTable: "WBHK_OTCM_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "ORDR",
                table: "PAYM_RVW_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "AMNT", "The gateway amount is not the order total", true, "Amount mismatch", (short)1 },
                    { (short)2, "LATE", "Money arrived for an order that was already cancelled or expired", true, "Order not payable", (short)2 },
                    { (short)3, "DUPL", "A second attempt was charged after the order was already paid", true, "Duplicate charge", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "ORDR",
                table: "PAYM_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "INIT", "The payment exists but no gateway session has been opened", true, "Initiated", (short)1 },
                    { (short)2, "PEND", "Waiting for the gateway (online) or for the courier (cash on delivery)", true, "Pending", (short)2 },
                    { (short)3, "PAID", "The gateway confirmed the full amount", true, "Paid", (short)3 },
                    { (short)4, "FAIL", "The gateway declined; the shopper may try again", true, "Failed", (short)4 },
                    { (short)5, "EXPR", "The payment window passed without payment", true, "Expired", (short)5 },
                    { (short)6, "REVW", "Held for staff: wrong amount, late payment or a duplicate charge", true, "In review", (short)6 }
                });

            migrationBuilder.InsertData(
                schema: "ORDR",
                table: "WBHK_OTCM_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "RJCT", "Missing, bad or stale signature, or unreadable", true, "Rejected", (short)1 },
                    { (short)2, "APLD", "Verified and applied to the payment", true, "Applied", (short)2 },
                    { (short)3, "DUPL", "The same event was already handled", true, "Duplicate", (short)3 },
                    { (short)4, "UNMT", "Verified, but about a payment attempt we do not know", true, "Unmatched", (short)4 },
                    { (short)5, "FLAG", "Verified, but held for staff review", true, "Flagged", (short)5 },
                    { (short)6, "IGNR", "Verified, but nothing to change", true, "Ignored", (short)6 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_ATMP_PAYM_STS_ID_STRT_DTE",
                schema: "ORDR",
                table: "PAYM_ATMP",
                columns: new[] { "PAYM_STS_ID", "STRT_DTE" });

            migrationBuilder.CreateIndex(
                name: "UQ_PAYM_ATMP_PAYM_ID_ATMP_NO",
                schema: "ORDR",
                table: "PAYM_ATMP",
                columns: new[] { "PAYM_ID", "ATMP_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_PAYM_ATMP_PRVD_REF",
                schema: "ORDR",
                table: "PAYM_ATMP",
                column: "PRVD_REF",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_MAIN_PAYM_METH_ID",
                schema: "ORDR",
                table: "PAYM_MAIN",
                column: "PAYM_METH_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_MAIN_PAYM_STS_ID_WNDW_END_DTE",
                schema: "ORDR",
                table: "PAYM_MAIN",
                columns: new[] { "PAYM_STS_ID", "WNDW_END_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_MAIN_RVW_RSN_ID",
                schema: "ORDR",
                table: "PAYM_MAIN",
                column: "RVW_RSN_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_PAYM_MAIN_ORDR_ID",
                schema: "ORDR",
                table: "PAYM_MAIN",
                column: "ORDR_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_RVW_LKP_CODE",
                schema: "ORDR",
                table: "PAYM_RVW_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_STS_LKP_CODE",
                schema: "ORDR",
                table: "PAYM_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_WBHK_PAYM_ID",
                schema: "ORDR",
                table: "PAYM_WBHK",
                column: "PAYM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_WBHK_RCVD_DTE",
                schema: "ORDR",
                table: "PAYM_WBHK",
                column: "RCVD_DTE");

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_WBHK_WBHK_OTCM_ID",
                schema: "ORDR",
                table: "PAYM_WBHK",
                column: "WBHK_OTCM_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_PAYM_WBHK_PRVD_EVNT_ID",
                schema: "ORDR",
                table: "PAYM_WBHK",
                columns: new[] { "PRVD", "EVNT_ID" },
                unique: true,
                filter: "\"EVNT_ID\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WBHK_OTCM_LKP_CODE",
                schema: "ORDR",
                table: "WBHK_OTCM_LKP",
                column: "CODE",
                unique: true);
            // Orders placed before payments existed get the row they would have been given: cash on delivery orders are owed on delivery (Pending).
            migrationBuilder.Sql("""
                INSERT INTO "ORDR"."PAYM_MAIN" ("ORDR_ID", "PAYM_METH_ID", "PAYM_STS_ID", "AMT", "INSR_DTE", "INSR_BY")
                SELECT o."ID", o."PAYM_METH_ID", CASE WHEN o."PAYM_METH_ID" = 1 THEN 2 ELSE 1 END, o."TOT_AMT", now(), 'migration'
                FROM "ORDR"."ORDR_MAIN" o
                WHERE NOT EXISTS (SELECT 1 FROM "ORDR"."PAYM_MAIN" p WHERE p."ORDR_ID" = o."ID")
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PAYM_ATMP",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "PAYM_WBHK",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "PAYM_MAIN",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "WBHK_OTCM_LKP",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "PAYM_RVW_LKP",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "PAYM_STS_LKP",
                schema: "ORDR");
        }
    }
}
