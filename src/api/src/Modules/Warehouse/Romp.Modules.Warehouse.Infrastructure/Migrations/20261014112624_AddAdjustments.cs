using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Warehouse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADJ_RSN_LKP",
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
                    table.PrimaryKey("PK_ADJ_RSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ADJ_MAIN",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VRNT_REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    GRD_ID = table.Column<short>(type: "smallint", nullable: false),
                    BIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    QTY_CHNG = table.Column<int>(type: "integer", nullable: false),
                    ADJ_RSN_ID = table.Column<short>(type: "smallint", nullable: false),
                    NOTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RCNF_IND = table.Column<bool>(type: "boolean", nullable: false),
                    IDEM_KEY = table.Column<Guid>(type: "uuid", nullable: false),
                    CNT_LINE_ID = table.Column<long>(type: "bigint", nullable: true),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADJ_MAIN", x => x.ID);
                    table.CheckConstraint("CK_ADJ_MAIN_QTY_CHNG_NONZERO", "\"QTY_CHNG\" <> 0");
                    table.ForeignKey(
                        name: "FK_ADJ_MAIN_ADJ_RSN_LKP",
                        column: x => x.ADJ_RSN_ID,
                        principalSchema: "WHSE",
                        principalTable: "ADJ_RSN_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ADJ_MAIN_BIN_MAIN",
                        column: x => x.BIN_ID,
                        principalSchema: "WHSE",
                        principalTable: "BIN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ADJ_MAIN_GRD_LKP",
                        column: x => x.GRD_ID,
                        principalSchema: "WHSE",
                        principalTable: "GRD_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ADJ_MAIN_VRNT_REF",
                        column: x => x.VRNT_REF_ID,
                        principalSchema: "WHSE",
                        principalTable: "VRNT_REF",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "WHSE",
                table: "ADJ_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "DAMAGE", "Units damaged on the shelf or in handling", true, "Damage", (short)1 },
                    { (short)2, "MISCOUNT", "The system count was wrong", true, "Miscount", (short)2 },
                    { (short)3, "THEFT", "Units are missing and believed stolen", true, "Theft", (short)3 },
                    { (short)4, "FOUND_STOCK", "Units found that the system did not know about", true, "Found stock", (short)4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ADJ_MAIN_ADJ_RSN_ID",
                schema: "WHSE",
                table: "ADJ_MAIN",
                column: "ADJ_RSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ADJ_MAIN_BIN_ID",
                schema: "WHSE",
                table: "ADJ_MAIN",
                column: "BIN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ADJ_MAIN_GRD_ID",
                schema: "WHSE",
                table: "ADJ_MAIN",
                column: "GRD_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ADJ_MAIN_IDEM_KEY",
                schema: "WHSE",
                table: "ADJ_MAIN",
                column: "IDEM_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ADJ_MAIN_INSR_DTE",
                schema: "WHSE",
                table: "ADJ_MAIN",
                column: "INSR_DTE");

            migrationBuilder.CreateIndex(
                name: "IX_ADJ_MAIN_VRNT_REF_ID_ID",
                schema: "WHSE",
                table: "ADJ_MAIN",
                columns: new[] { "VRNT_REF_ID", "ID" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ADJ_RSN_LKP_CODE",
                schema: "WHSE",
                table: "ADJ_RSN_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADJ_MAIN",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "ADJ_RSN_LKP",
                schema: "WHSE");
        }
    }
}
