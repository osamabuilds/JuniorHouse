using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814
#pragma warning disable CA1861 // generated migration: constant arrays are fine here

namespace Romp.Modules.Shipping.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "COD_OVRD_DTE",
                schema: "SHPG",
                table: "SHPG_MAIN",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "COD_STS_ID",
                schema: "SHPG",
                table: "SHPG_MAIN",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "COD_STS_LKP",
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
                    table.PrimaryKey("PK_COD_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RMTN_MAIN",
                schema: "SHPG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CRER_ID = table.Column<short>(type: "smallint", nullable: false),
                    REF_NO = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RMTN_DTE = table.Column<DateOnly>(type: "date", nullable: false),
                    TOT_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RMTN_MAIN", x => x.ID);
                    table.CheckConstraint("CK_RMTN_MAIN_TOT_AMT", "\"TOT_AMT\" > 0");
                    table.ForeignKey(
                        name: "FK_RMTN_MAIN_CRER_LKP",
                        column: x => x.CRER_ID,
                        principalSchema: "SHPG",
                        principalTable: "CRER_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RMTN_OTCM_LKP",
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
                    table.PrimaryKey("PK_RMTN_OTCM_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RMTN_LINE",
                schema: "SHPG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RMTN_ID = table.Column<long>(type: "bigint", nullable: false),
                    WYBL_NO = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    SHPG_ID = table.Column<long>(type: "bigint", nullable: true),
                    RMTN_OTCM_ID = table.Column<short>(type: "smallint", nullable: false),
                    RSLV_NOTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RSLV_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RSLV_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RMTN_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RMTN_LINE_RMTN_MAIN",
                        column: x => x.RMTN_ID,
                        principalSchema: "SHPG",
                        principalTable: "RMTN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RMTN_LINE_RMTN_OTCM_LKP",
                        column: x => x.RMTN_OTCM_ID,
                        principalSchema: "SHPG",
                        principalTable: "RMTN_OTCM_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RMTN_LINE_SHPG_MAIN",
                        column: x => x.SHPG_ID,
                        principalSchema: "SHPG",
                        principalTable: "SHPG_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "SHPG",
                table: "COD_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "NOT_COD", "Nothing to collect in cash", true, "Not COD", (short)1 },
                    { (short)2, "AWAITING", "Cash to collect; no courier statement has paid it", true, "Awaiting cash", (short)2 },
                    { (short)3, "SETTLED", "A courier statement paid exactly the cash", true, "Settled", (short)3 },
                    { (short)4, "SHORT", "The courier paid less than the cash", true, "Short", (short)4 },
                    { (short)5, "OVER", "The courier paid more than the cash", true, "Over", (short)5 },
                    { (short)6, "RESOLVED", "A short or over payment closed by staff with a note", true, "Resolved", (short)6 }
                });

            migrationBuilder.InsertData(
                schema: "SHPG",
                table: "RMTN_OTCM_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "MATCHED", "Equals the cash to collect", true, "Matched", (short)1 },
                    { (short)2, "SHORT", "Less than the cash to collect", true, "Short", (short)2 },
                    { (short)3, "OVER", "More than the cash to collect", true, "Over", (short)3 },
                    { (short)4, "DUPLICATE", "The parcel was already paid", true, "Paid before", (short)4 },
                    { (short)5, "UNKNOWN", "No parcel of this courier has this waybill", true, "Unknown waybill", (short)5 },
                    { (short)6, "NOT_DELIVERED", "The parcel is not delivered", true, "Not delivered yet", (short)6 },
                    { (short)7, "NOT_COD", "The parcel had no cash to collect", true, "Not a cash parcel", (short)7 }
                });

            // Existing shipments: nothing to collect is NOT_COD (1), cash to collect is AWAITING (2).
            migrationBuilder.Sql("UPDATE \"SHPG\".\"SHPG_MAIN\" SET \"COD_STS_ID\" = CASE WHEN \"CASH_AMT\" > 0 THEN 2 ELSE 1 END");

            migrationBuilder.CreateIndex(
                name: "IX_SHPG_MAIN_COD_STS_ID_DLVR_DTE",
                schema: "SHPG",
                table: "SHPG_MAIN",
                columns: new[] { "COD_STS_ID", "DLVR_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_COD_STS_LKP_CODE",
                schema: "SHPG",
                table: "COD_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RMTN_LINE_RMTN_ID",
                schema: "SHPG",
                table: "RMTN_LINE",
                column: "RMTN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_RMTN_LINE_RMTN_OTCM_ID_RSLV_DTE",
                schema: "SHPG",
                table: "RMTN_LINE",
                columns: new[] { "RMTN_OTCM_ID", "RSLV_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_RMTN_LINE_SHPG_ID",
                schema: "SHPG",
                table: "RMTN_LINE",
                column: "SHPG_ID");

            migrationBuilder.CreateIndex(
                name: "IX_RMTN_MAIN_RMTN_DTE",
                schema: "SHPG",
                table: "RMTN_MAIN",
                column: "RMTN_DTE");

            migrationBuilder.CreateIndex(
                name: "UQ_RMTN_MAIN_CRER_ID_REF_NO",
                schema: "SHPG",
                table: "RMTN_MAIN",
                columns: new[] { "CRER_ID", "REF_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RMTN_OTCM_LKP_CODE",
                schema: "SHPG",
                table: "RMTN_OTCM_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SHPG_MAIN_COD_STS_LKP",
                schema: "SHPG",
                table: "SHPG_MAIN",
                column: "COD_STS_ID",
                principalSchema: "SHPG",
                principalTable: "COD_STS_LKP",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SHPG_MAIN_COD_STS_LKP",
                schema: "SHPG",
                table: "SHPG_MAIN");

            migrationBuilder.DropTable(
                name: "COD_STS_LKP",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "RMTN_LINE",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "RMTN_MAIN",
                schema: "SHPG");

            migrationBuilder.DropTable(
                name: "RMTN_OTCM_LKP",
                schema: "SHPG");

            migrationBuilder.DropIndex(
                name: "IX_SHPG_MAIN_COD_STS_ID_DLVR_DTE",
                schema: "SHPG",
                table: "SHPG_MAIN");

            migrationBuilder.DropColumn(
                name: "COD_OVRD_DTE",
                schema: "SHPG",
                table: "SHPG_MAIN");

            migrationBuilder.DropColumn(
                name: "COD_STS_ID",
                schema: "SHPG",
                table: "SHPG_MAIN");
        }
    }
}
