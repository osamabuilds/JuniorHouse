using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814
#pragma warning disable CA1861 // generated migration: constant arrays are fine here

namespace Romp.Modules.Returns.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "RTRN");

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "RTRN",
                columns: table => new
                {
                    MSG_ID = table.Column<long>(type: "bigint", nullable: false),
                    HNDL_NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PROC_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INBX", x => new { x.MSG_ID, x.HNDL_NAME });
                });

            migrationBuilder.CreateTable(
                name: "OUTB_MSG",
                schema: "RTRN",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EVNT_TYP = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PYLD = table.Column<string>(type: "jsonb", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AGGR_TYP = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PurchaseOrder"),
                    AGGR_ID = table.Column<long>(type: "bigint", nullable: true),
                    MSG_VER = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    ATMP_CNT = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    NXT_ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CLM_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LEAS_EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PROC_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DEDL_IND = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DEDL_RSN = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OUTB_MSG", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RTRN_DCSN_LKP",
                schema: "RTRN",
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
                    table.PrimaryKey("PK_RTRN_DCSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RTRN_KIND_LKP",
                schema: "RTRN",
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
                    table.PrimaryKey("PK_RTRN_KIND_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RTRN_STS_LKP",
                schema: "RTRN",
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
                    table.PrimaryKey("PK_RTRN_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RTRN_MAIN",
                schema: "RTRN",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    ORDR_NO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RTRN_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    RTRN_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    SEAL_INTC_IND = table.Column<bool>(type: "boolean", nullable: true),
                    BIN_CODE = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    RCVD_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RCVD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CMPL_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CMPL_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RTRN_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RTRN_MAIN_RTRN_KIND_LKP",
                        column: x => x.RTRN_KIND_ID,
                        principalSchema: "RTRN",
                        principalTable: "RTRN_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RTRN_MAIN_RTRN_STS_LKP",
                        column: x => x.RTRN_STS_ID,
                        principalSchema: "RTRN",
                        principalTable: "RTRN_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RTRN_LINE",
                schema: "RTRN",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RTRN_ID = table.Column<long>(type: "bigint", nullable: false),
                    VRNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    SKU = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CLR = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SIZE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ORDR_QTY = table.Column<int>(type: "integer", nullable: false),
                    RTRN_DCSN_ID = table.Column<short>(type: "smallint", nullable: true),
                    DCSN_QTY = table.Column<int>(type: "integer", nullable: true),
                    NOTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DCSN_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DCSN_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RTRN_LINE", x => x.ID);
                    table.CheckConstraint("CK_RTRN_LINE_DCSN_QTY", "\"DCSN_QTY\" IS NULL OR (\"DCSN_QTY\" >= 1 AND \"DCSN_QTY\" <= \"ORDR_QTY\")");
                    table.CheckConstraint("CK_RTRN_LINE_ORDR_QTY", "\"ORDR_QTY\" > 0");
                    table.ForeignKey(
                        name: "FK_RTRN_LINE_RTRN_DCSN_LKP",
                        column: x => x.RTRN_DCSN_ID,
                        principalSchema: "RTRN",
                        principalTable: "RTRN_DCSN_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RTRN_LINE_RTRN_MAIN",
                        column: x => x.RTRN_ID,
                        principalSchema: "RTRN",
                        principalTable: "RTRN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "RTRN",
                table: "RTRN_DCSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "RESTOCK", "Back to sellable stock", true, "Restock", (short)1 },
                    { (short)2, "SECONDS", "Back to stock as graded (concession) units", true, "Restock as seconds", (short)2 },
                    { (short)3, "REJECT", "Not restocked; a person resolves it", true, "Reject", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "RTRN",
                table: "RTRN_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[] { (short)1, "RTO", "The parcel came back to us from the courier", true, "Returned to origin", (short)1 });

            migrationBuilder.InsertData(
                schema: "RTRN",
                table: "RTRN_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "AWAITING_RECEIPT", "Waiting for the parcel to be received at the warehouse", true, "Awaiting receipt", (short)1 },
                    { (short)2, "IN_INSPECTION", "Received; lines are being decided", true, "In inspection", (short)2 },
                    { (short)3, "COMPLETED", "Every line is decided; the case is frozen", true, "Completed", (short)3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RTRN_DCSN_LKP_CODE",
                schema: "RTRN",
                table: "RTRN_DCSN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RTRN_KIND_LKP_CODE",
                schema: "RTRN",
                table: "RTRN_KIND_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RTRN_LINE_RTRN_DCSN_ID",
                schema: "RTRN",
                table: "RTRN_LINE",
                column: "RTRN_DCSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_RTRN_LINE_RTRN_ID",
                schema: "RTRN",
                table: "RTRN_LINE",
                column: "RTRN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_RTRN_MAIN_RTRN_KIND_ID",
                schema: "RTRN",
                table: "RTRN_MAIN",
                column: "RTRN_KIND_ID");

            migrationBuilder.CreateIndex(
                name: "IX_RTRN_MAIN_RTRN_STS_ID_INSR_DTE",
                schema: "RTRN",
                table: "RTRN_MAIN",
                columns: new[] { "RTRN_STS_ID", "INSR_DTE" });

            migrationBuilder.CreateIndex(
                name: "UQ_RTRN_MAIN_ORDR_ID_RTRN_KIND_ID",
                schema: "RTRN",
                table: "RTRN_MAIN",
                columns: new[] { "ORDR_ID", "RTRN_KIND_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RTRN_STS_LKP_CODE",
                schema: "RTRN",
                table: "RTRN_STS_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INBX",
                schema: "RTRN");

            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "RTRN");

            migrationBuilder.DropTable(
                name: "RTRN_LINE",
                schema: "RTRN");

            migrationBuilder.DropTable(
                name: "RTRN_DCSN_LKP",
                schema: "RTRN");

            migrationBuilder.DropTable(
                name: "RTRN_MAIN",
                schema: "RTRN");

            migrationBuilder.DropTable(
                name: "RTRN_KIND_LKP",
                schema: "RTRN");

            migrationBuilder.DropTable(
                name: "RTRN_STS_LKP",
                schema: "RTRN");
        }
    }
}
