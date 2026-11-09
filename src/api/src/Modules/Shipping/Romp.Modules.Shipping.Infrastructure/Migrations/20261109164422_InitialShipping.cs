using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814
#pragma warning disable CA1861 // generated migration: constant arrays are fine here

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Shipping.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialShipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "SHPG");

            migrationBuilder.CreateTable(
                name: "BKNG_SRC_LKP",
                schema: "SHPG",
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
                    table.PrimaryKey("PK_BKNG_SRC_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CRER_LKP",
                schema: "SHPG",
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
                    table.PrimaryKey("PK_CRER_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "EVNT_SRC_LKP",
                schema: "SHPG",
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
                    table.PrimaryKey("PK_EVNT_SRC_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "SHPG",
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
                name: "MNFS_STS_LKP",
                schema: "SHPG",
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
                    table.PrimaryKey("PK_MNFS_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OUTB_MSG",
                schema: "SHPG",
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
                name: "SHPG_STS_LKP",
                schema: "SHPG",
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
                    table.PrimaryKey("PK_SHPG_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MNFS_MAIN",
                schema: "SHPG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CRER_ID = table.Column<short>(type: "smallint", nullable: false),
                    MNFS_DTE = table.Column<DateOnly>(type: "date", nullable: false),
                    MNFS_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    CNTD_CNT = table.Column<int>(type: "integer", nullable: true),
                    CNTD_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CNTD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NOTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MNFS_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MNFS_MAIN_CRER_LKP",
                        column: x => x.CRER_ID,
                        principalSchema: "SHPG",
                        principalTable: "CRER_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MNFS_MAIN_MNFS_STS_LKP",
                        column: x => x.MNFS_STS_ID,
                        principalSchema: "SHPG",
                        principalTable: "MNFS_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SHPG_MAIN",
                schema: "SHPG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    ORDR_NO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SHPG_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    STS_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CRER_ID = table.Column<short>(type: "smallint", nullable: true),
                    WYBL_NO = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    BKNG_SRC_ID = table.Column<short>(type: "smallint", nullable: true),
                    BKNG_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CASH_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    WGHT_GRM = table.Column<int>(type: "integer", nullable: false),
                    ATMP_CNT = table.Column<int>(type: "integer", nullable: false),
                    LAST_ERR = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LAST_NOTE = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NXT_ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NEED_STAF_IND = table.Column<bool>(type: "boolean", nullable: false),
                    FAIL_ATMP_CNT = table.Column<int>(type: "integer", nullable: false),
                    DLVR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MNFS_ID = table.Column<long>(type: "bigint", nullable: true),
                    LBL_PRT_CNT = table.Column<int>(type: "integer", nullable: false),
                    POLL_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SHPG_MAIN", x => x.ID);
                    table.CheckConstraint("CK_SHPG_MAIN_CASH_AMT", "\"CASH_AMT\" >= 0");
                    table.CheckConstraint("CK_SHPG_MAIN_WGHT_GRM", "\"WGHT_GRM\" > 0");
                    table.ForeignKey(
                        name: "FK_SHPG_MAIN_BKNG_SRC_LKP",
                        column: x => x.BKNG_SRC_ID,
                        principalSchema: "SHPG",
                        principalTable: "BKNG_SRC_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SHPG_MAIN_CRER_LKP",
                        column: x => x.CRER_ID,
                        principalSchema: "SHPG",
                        principalTable: "CRER_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SHPG_MAIN_MNFS_MAIN",
                        column: x => x.MNFS_ID,
                        principalSchema: "SHPG",
                        principalTable: "MNFS_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SHPG_MAIN_SHPG_STS_LKP",
                        column: x => x.SHPG_STS_ID,
                        principalSchema: "SHPG",
                        principalTable: "SHPG_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MNFS_LINE",
                schema: "SHPG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MNFS_ID = table.Column<long>(type: "bigint", nullable: false),
                    SHPG_ID = table.Column<long>(type: "bigint", nullable: false),
                    HAND_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MNFS_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MNFS_LINE_MNFS_MAIN",
                        column: x => x.MNFS_ID,
                        principalSchema: "SHPG",
                        principalTable: "MNFS_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MNFS_LINE_SHPG_MAIN",
                        column: x => x.SHPG_ID,
                        principalSchema: "SHPG",
                        principalTable: "SHPG_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SHPG_EVNT",
                schema: "SHPG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SHPG_ID = table.Column<long>(type: "bigint", nullable: false),
                    SHPG_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    CRER_EVNT_ID = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    EVNT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EVNT_SRC_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SHPG_EVNT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SHPG_EVNT_EVNT_SRC_LKP",
                        column: x => x.EVNT_SRC_ID,
                        principalSchema: "SHPG",
                        principalTable: "EVNT_SRC_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SHPG_EVNT_SHPG_MAIN",
                        column: x => x.SHPG_ID,
                        principalSchema: "SHPG",
                        principalTable: "SHPG_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SHPG_EVNT_SHPG_STS_LKP",
                        column: x => x.SHPG_STS_ID,
                        principalSchema: "SHPG",
                        principalTable: "SHPG_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "SHPG",
                table: "BKNG_SRC_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "COURIER", "The waybill came from the courier's booking", true, "Courier", (short)1 },
                    { (short)2, "MANUAL", "Staff entered the waybill", true, "Manual", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "SHPG",
                table: "CRER_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "PRIMARY", "The default courier", true, "Primary courier", (short)1 },
                    { (short)2, "SECONDARY", "Takes the parcel when the primary cannot be reached", true, "Secondary courier", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "SHPG",
                table: "EVNT_SRC_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "PUSH", "The courier sent it", true, "Push", (short)1 },
                    { (short)2, "POLL", "We asked the courier", true, "Poll", (short)2 },
                    { (short)3, "SYSTEM", "Raised by Romp", true, "System", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "SHPG",
                table: "MNFS_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "OPEN", "Taking parcels; not counted yet", true, "Open", (short)1 },
                    { (short)2, "MATCHED", "The courier took exactly the listed parcels", true, "Matched", (short)2 },
                    { (short)3, "MISMATCH", "The count differs; staff resolve it", true, "Mismatch", (short)3 },
                    { (short)4, "CLOSED", "A mismatch that was resolved", true, "Closed", (short)4 }
                });

            migrationBuilder.InsertData(
                schema: "SHPG",
                table: "SHPG_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "PENDING_BOOKING", "Waiting for a courier to take it", true, "Pending booking", (short)1 },
                    { (short)2, "BOOKED", "A courier gave a waybill", true, "Booked", (short)2 },
                    { (short)3, "PICKED_UP", "The courier collected it", true, "Picked up", (short)3 },
                    { (short)4, "IN_TRANSIT", "On its way", true, "In transit", (short)4 },
                    { (short)5, "OUT_FOR_DELIVERY", "With the rider", true, "Out for delivery", (short)5 },
                    { (short)6, "FAILED_ATTEMPT", "The delivery attempt failed", true, "Failed attempt", (short)6 },
                    { (short)7, "DELIVERED", "Handed to the customer", true, "Delivered", (short)7 },
                    { (short)8, "RETURNED_TO_ORIGIN", "Back with us unopened", true, "Returned to origin", (short)8 },
                    { (short)9, "CANCELLED", "The order was cancelled before pick-up", true, "Cancelled", (short)9 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BKNG_SRC_LKP_CODE",
                schema: "SHPG",
                table: "BKNG_SRC_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CRER_LKP_CODE",
                schema: "SHPG",
                table: "CRER_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EVNT_SRC_LKP_CODE",
                schema: "SHPG",
                table: "EVNT_SRC_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MNFS_LINE_SHPG_ID",
                schema: "SHPG",
                table: "MNFS_LINE",
                column: "SHPG_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_MNFS_LINE_MNFS_ID_SHPG_ID",
                schema: "SHPG",
                table: "MNFS_LINE",
                columns: new[] { "MNFS_ID", "SHPG_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MNFS_MAIN_MNFS_STS_ID",
                schema: "SHPG",
                table: "MNFS_MAIN",
                column: "MNFS_STS_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_MNFS_MAIN_CRER_ID_MNFS_DTE",
                schema: "SHPG",
                table: "MNFS_MAIN",
                columns: new[] { "CRER_ID", "MNFS_DTE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MNFS_STS_LKP_CODE",
                schema: "SHPG",
                table: "MNFS_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SHPG_EVNT_EVNT_SRC_ID",
                schema: "SHPG",
                table: "SHPG_EVNT",
                column: "EVNT_SRC_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SHPG_EVNT_SHPG_STS_ID",
                schema: "SHPG",
                table: "SHPG_EVNT",
                column: "SHPG_STS_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_SHPG_EVNT_SHPG_ID_CRER_EVNT_ID",
                schema: "SHPG",
                table: "SHPG_EVNT",
                columns: new[] { "SHPG_ID", "CRER_EVNT_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SHPG_MAIN_BKNG_SRC_ID",
                schema: "SHPG",
                table: "SHPG_MAIN",
                column: "BKNG_SRC_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SHPG_MAIN_CRER_ID_SHPG_STS_ID",
                schema: "SHPG",
                table: "SHPG_MAIN",
                columns: new[] { "CRER_ID", "SHPG_STS_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_SHPG_MAIN_MNFS_ID",
                schema: "SHPG",
                table: "SHPG_MAIN",
                column: "MNFS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SHPG_MAIN_SHPG_STS_ID_NXT_ATMP_DTE",
                schema: "SHPG",
                table: "SHPG_MAIN",
                columns: new[] { "SHPG_STS_ID", "NXT_ATMP_DTE" });

            migrationBuilder.CreateIndex(
                name: "UQ_SHPG_MAIN_ORDR_ID",
                schema: "SHPG",
                table: "SHPG_MAIN",
                column: "ORDR_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SHPG_MAIN_WYBL_NO",
                schema: "SHPG",
                table: "SHPG_MAIN",
                column: "WYBL_NO",
                unique: true,
                filter: "\"WYBL_NO\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SHPG_STS_LKP_CODE",
                schema: "SHPG",
                table: "SHPG_STS_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INBX",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "MNFS_LINE",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "SHPG_EVNT",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "EVNT_SRC_LKP",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "SHPG_MAIN",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "BKNG_SRC_LKP",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "MNFS_MAIN",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "SHPG_STS_LKP",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "CRER_LKP",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "MNFS_STS_LKP",
                schema: "SHPG");
        }
    }
}
