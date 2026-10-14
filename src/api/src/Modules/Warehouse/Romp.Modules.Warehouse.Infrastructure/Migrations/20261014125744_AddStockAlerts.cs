using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Warehouse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ALRT_LVL_LKP",
                schema: "WHSE",
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
                    table.PrimaryKey("PK_ALRT_LVL_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ALRT_STS_LKP",
                schema: "WHSE",
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
                    table.PrimaryKey("PK_ALRT_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ALRT_THRS",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VRNT_REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    THRS_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ALRT_THRS", x => x.ID);
                    table.CheckConstraint("CK_ALRT_THRS_QTY_RANGE", "\"THRS_QTY\" >= 0 AND \"THRS_QTY\" <= 100000");
                    table.ForeignKey(
                        name: "FK_ALRT_THRS_VRNT_REF",
                        column: x => x.VRNT_REF_ID,
                        principalSchema: "WHSE",
                        principalTable: "VRNT_REF",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ALRT_MAIN",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VRNT_REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    ALRT_LVL_ID = table.Column<short>(type: "smallint", nullable: false),
                    ALRT_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    LAST_QTY = table.Column<int>(type: "integer", nullable: false),
                    THRS_QTY = table.Column<int>(type: "integer", nullable: false),
                    OPEN_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CLSD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ACK_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ACK_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ALRT_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ALRT_MAIN_ALRT_LVL_LKP",
                        column: x => x.ALRT_LVL_ID,
                        principalSchema: "WHSE",
                        principalTable: "ALRT_LVL_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ALRT_MAIN_ALRT_STS_LKP",
                        column: x => x.ALRT_STS_ID,
                        principalSchema: "WHSE",
                        principalTable: "ALRT_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ALRT_MAIN_VRNT_REF",
                        column: x => x.VRNT_REF_ID,
                        principalSchema: "WHSE",
                        principalTable: "VRNT_REF",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "WHSE",
                table: "ALRT_LVL_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "LOW", "Available is at or below the threshold", true, "Low stock", (short)1 },
                    { (short)2, "OUT", "Nothing available", true, "Stock-out", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "WHSE",
                table: "ALRT_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "OPEN", "Stock is still low", true, "Open", (short)1 },
                    { (short)2, "CLOSED", "Stock recovered above the threshold", true, "Closed", (short)2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ALRT_LVL_LKP_CODE",
                schema: "WHSE",
                table: "ALRT_LVL_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ALRT_MAIN_ALRT_LVL_ID",
                schema: "WHSE",
                table: "ALRT_MAIN",
                column: "ALRT_LVL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ALRT_MAIN_ALRT_STS_ID_ID",
                schema: "WHSE",
                table: "ALRT_MAIN",
                columns: new[] { "ALRT_STS_ID", "ID" });

            migrationBuilder.CreateIndex(
                name: "UX_ALRT_MAIN_OPEN",
                schema: "WHSE",
                table: "ALRT_MAIN",
                column: "VRNT_REF_ID",
                unique: true,
                filter: "\"ALRT_STS_ID\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ALRT_STS_LKP_CODE",
                schema: "WHSE",
                table: "ALRT_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ALRT_THRS_VRNT_REF_ID",
                schema: "WHSE",
                table: "ALRT_THRS",
                column: "VRNT_REF_ID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ALRT_MAIN",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "ALRT_THRS",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "ALRT_LVL_LKP",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "ALRT_STS_LKP",
                schema: "WHSE");
        }
    }
}
