using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Romp.Modules.Vendor.Domain.Vendors;

#nullable disable

namespace Romp.Modules.Vendor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialVendorsAndPurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PO_MAIN",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_NO = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    VNDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    STYL_ID = table.Column<long>(type: "bigint", nullable: false),
                    UNIT_COST_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    EXPC_DLVR_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    PAYM_TERM_ID = table.Column<short>(type: "smallint", nullable: false),
                    ADV_PCT = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    PO_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_MAIN", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PO_NO_SEQ",
                schema: "VNDR",
                columns: table => new
                {
                    YR = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SEQ = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_NO_SEQ", x => x.YR);
                });

            migrationBuilder.CreateTable(
                name: "VNDR",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VNDR_NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CNTC_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CNTC_PHON = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CNTC_EML = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CITY_ID = table.Column<short>(type: "smallint", nullable: false),
                    PAYM_TERM_ID = table.Column<short>(type: "smallint", nullable: false),
                    ONTM_PCT = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    ONQT_PCT = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    DFCT_RATE_PCT = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VNDR", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PO_LINE",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PO_LINE_PO_ID",
                        column: x => x.PO_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PO_STS_HIST",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    PO_CNCL_RSN_ID = table.Column<short>(type: "smallint", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_STS_HIST", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PO_STS_HIST_PO_ID",
                        column: x => x.PO_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VNDR_SPCL_MAP",
                schema: "VNDR",
                columns: table => new
                {
                    VNDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    SPCL_ID = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VNDR_SPCL_MAP", x => new { x.VNDR_ID, x.SPCL_ID });
                    table.ForeignKey(
                        name: "FK_VNDR_SPCL_MAP_VNDR_ID",
                        column: x => x.VNDR_ID,
                        principalSchema: "VNDR",
                        principalTable: "VNDR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PO_LINE_PO_ID_SIZE_ID_CLR_ID",
                schema: "VNDR",
                table: "PO_LINE",
                columns: new[] { "PO_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PO_MAIN_PAYM_TERM_ID",
                schema: "VNDR",
                table: "PO_MAIN",
                column: "PAYM_TERM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_MAIN_PO_NO",
                schema: "VNDR",
                table: "PO_MAIN",
                column: "PO_NO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PO_MAIN_PO_STS_ID",
                schema: "VNDR",
                table: "PO_MAIN",
                column: "PO_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_MAIN_STYL_ID",
                schema: "VNDR",
                table: "PO_MAIN",
                column: "STYL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_MAIN_VNDR_ID",
                schema: "VNDR",
                table: "PO_MAIN",
                column: "VNDR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_STS_HIST_PO_ID",
                schema: "VNDR",
                table: "PO_STS_HIST",
                column: "PO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VNDR_CITY_ID",
                schema: "VNDR",
                table: "VNDR",
                column: "CITY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VNDR_PAYM_TERM_ID",
                schema: "VNDR",
                table: "VNDR",
                column: "PAYM_TERM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VNDR_SPCL_MAP_SPCL_ID",
                schema: "VNDR",
                table: "VNDR_SPCL_MAP",
                column: "SPCL_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PO_LINE",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "PO_NO_SEQ",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "PO_STS_HIST",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "VNDR_SPCL_MAP",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "PO_MAIN",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "VNDR",
                schema: "VNDR");
        }
    }
}
