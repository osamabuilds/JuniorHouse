using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

namespace Romp.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "COD_ATMP_CNT",
                schema: "ORDR",
                table: "ORDR_MAIN",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "COD_CNCL_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "COD_DUE_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "COD_WHTS_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FRST_ORDR_IND",
                schema: "ORDR",
                table: "ORDR_MAIN",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HI_VAL_IND",
                schema: "ORDR",
                table: "ORDR_MAIN",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RFSL_FLG_IND",
                schema: "ORDR",
                table: "ORDR_MAIN",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "COD_OTCM_LKP",
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
                    table.PrimaryKey("PK_COD_OTCM_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "COD_RISK",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CNTC_PHON = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RFSL_CNT = table.Column<int>(type: "integer", nullable: false),
                    NOAN_CNCL_CNT = table.Column<int>(type: "integer", nullable: false),
                    FLG_IND = table.Column<bool>(type: "boolean", nullable: false),
                    CLRD_BY = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CLRD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COD_RISK", x => x.ID);
                    table.CheckConstraint("CK_COD_RISK_CNTS", "\"RFSL_CNT\" >= 0 AND \"NOAN_CNCL_CNT\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "COD_ATMP",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    ATMP_NO = table.Column<int>(type: "integer", nullable: false),
                    COD_OTCM_ID = table.Column<short>(type: "smallint", nullable: false),
                    ACTR_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    ACTR_REF = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NOTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OCCR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COD_ATMP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_COD_ATMP_ACTR_KIND",
                        column: x => x.ACTR_KIND_ID,
                        principalSchema: "ORDR",
                        principalTable: "ACTR_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COD_ATMP_COD_OTCM",
                        column: x => x.COD_OTCM_ID,
                        principalSchema: "ORDR",
                        principalTable: "COD_OTCM_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COD_ATMP_ORDR_MAIN",
                        column: x => x.ORDR_ID,
                        principalSchema: "ORDR",
                        principalTable: "ORDR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "ORDR",
                table: "COD_OTCM_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "CONF", "Staff reached the customer and the order is confirmed", true, "Confirmed by staff", (short)1 },
                    { (short)2, "NOAN", "Nobody answered the call", true, "No answer", (short)2 },
                    { (short)3, "REFU", "The customer refused the order", true, "Refused", (short)3 },
                    { (short)4, "CWAP", "The customer confirmed with the WhatsApp button", true, "Confirmed on WhatsApp", (short)4 },
                    { (short)5, "CWEB", "The customer confirmed with the button on the page or in the email", true, "Confirmed on the page", (short)5 },
                    { (short)6, "CCAN", "The customer cancelled with the WhatsApp button", true, "Cancelled on WhatsApp", (short)6 },
                    { (short)7, "CWCN", "The customer cancelled from the page or the email", true, "Cancelled on the page", (short)7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_MAIN_COD_CNCL_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN",
                column: "COD_CNCL_DTE",
                filter: "\"COD_CNCL_DTE\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_MAIN_COD_DUE_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN",
                column: "COD_DUE_DTE",
                filter: "\"COD_DUE_DTE\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_COD_ATMP_ACTR_KIND_ID",
                schema: "ORDR",
                table: "COD_ATMP",
                column: "ACTR_KIND_ID");

            migrationBuilder.CreateIndex(
                name: "IX_COD_ATMP_COD_OTCM_ID",
                schema: "ORDR",
                table: "COD_ATMP",
                column: "COD_OTCM_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_COD_ATMP_ORDR_ID_ATMP_NO",
                schema: "ORDR",
                table: "COD_ATMP",
                columns: new[] { "ORDR_ID", "ATMP_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COD_OTCM_LKP_CODE",
                schema: "ORDR",
                table: "COD_OTCM_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COD_RISK_FLG_IND",
                schema: "ORDR",
                table: "COD_RISK",
                column: "FLG_IND",
                filter: "\"FLG_IND\"");

            migrationBuilder.CreateIndex(
                name: "UQ_COD_RISK_CNTC_PHON",
                schema: "ORDR",
                table: "COD_RISK",
                column: "CNTC_PHON",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COD_ATMP",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "COD_RISK",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "COD_OTCM_LKP",
                schema: "ORDR");

            migrationBuilder.DropIndex(
                name: "IX_ORDR_MAIN_COD_CNCL_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN");

            migrationBuilder.DropIndex(
                name: "IX_ORDR_MAIN_COD_DUE_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN");

            migrationBuilder.DropColumn(
                name: "COD_ATMP_CNT",
                schema: "ORDR",
                table: "ORDR_MAIN");

            migrationBuilder.DropColumn(
                name: "COD_CNCL_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN");

            migrationBuilder.DropColumn(
                name: "COD_DUE_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN");

            migrationBuilder.DropColumn(
                name: "COD_WHTS_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN");

            migrationBuilder.DropColumn(
                name: "FRST_ORDR_IND",
                schema: "ORDR",
                table: "ORDR_MAIN");

            migrationBuilder.DropColumn(
                name: "HI_VAL_IND",
                schema: "ORDR",
                table: "ORDR_MAIN");

            migrationBuilder.DropColumn(
                name: "RFSL_FLG_IND",
                schema: "ORDR",
                table: "ORDR_MAIN");
        }
    }
}
