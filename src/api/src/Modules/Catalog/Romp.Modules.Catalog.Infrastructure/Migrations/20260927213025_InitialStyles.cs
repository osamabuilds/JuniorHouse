using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Romp.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialStyles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "CTLG");

            migrationBuilder.CreateTable(
                name: "STYL_MAIN",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    STYL_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    STYL_NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    COLN_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CATG_ID = table.Column<short>(type: "smallint", nullable: false),
                    GNDR_ID = table.Column<short>(type: "smallint", nullable: false),
                    AGE_BRKT_ID = table.Column<short>(type: "smallint", nullable: false),
                    FBRC_ID = table.Column<short>(type: "smallint", nullable: false),
                    TGT_UNIT_COST_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    TGT_RTL_PRIC_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STYL_MAIN", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "STYL_CLR_MAP",
                schema: "CTLG",
                columns: table => new
                {
                    STYL_ID = table.Column<long>(type: "bigint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STYL_CLR_MAP", x => new { x.STYL_ID, x.CLR_ID });
                    table.ForeignKey(
                        name: "FK_STYL_CLR_MAP_STYL_ID",
                        column: x => x.STYL_ID,
                        principalSchema: "CTLG",
                        principalTable: "STYL_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "STYL_SIZE_MAP",
                schema: "CTLG",
                columns: table => new
                {
                    STYL_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STYL_SIZE_MAP", x => new { x.STYL_ID, x.SIZE_ID });
                    table.ForeignKey(
                        name: "FK_STYL_SIZE_MAP_STYL_ID",
                        column: x => x.STYL_ID,
                        principalSchema: "CTLG",
                        principalTable: "STYL_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "STYL_TGT_LINE",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    STYL_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    TGT_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STYL_TGT_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_STYL_TGT_LINE_STYL_ID",
                        column: x => x.STYL_ID,
                        principalSchema: "CTLG",
                        principalTable: "STYL_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_STYL_CLR_MAP_CLR_ID",
                schema: "CTLG",
                table: "STYL_CLR_MAP",
                column: "CLR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STYL_MAIN_AGE_BRKT_ID",
                schema: "CTLG",
                table: "STYL_MAIN",
                column: "AGE_BRKT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STYL_MAIN_CATG_ID",
                schema: "CTLG",
                table: "STYL_MAIN",
                column: "CATG_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STYL_MAIN_FBRC_ID",
                schema: "CTLG",
                table: "STYL_MAIN",
                column: "FBRC_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STYL_MAIN_GNDR_ID",
                schema: "CTLG",
                table: "STYL_MAIN",
                column: "GNDR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STYL_MAIN_STYL_CODE",
                schema: "CTLG",
                table: "STYL_MAIN",
                column: "STYL_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_STYL_SIZE_MAP_SIZE_ID",
                schema: "CTLG",
                table: "STYL_SIZE_MAP",
                column: "SIZE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STYL_TGT_LINE_STYL_ID_SIZE_ID_CLR_ID",
                schema: "CTLG",
                table: "STYL_TGT_LINE",
                columns: new[] { "STYL_ID", "SIZE_ID", "CLR_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "STYL_CLR_MAP",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "STYL_SIZE_MAP",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "STYL_TGT_LINE",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "STYL_MAIN",
                schema: "CTLG");
        }
    }
}
