using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Warehouse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCycleCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CNT_NO_SEQ",
                schema: "WHSE",
                columns: table => new
                {
                    YR = table.Column<short>(type: "smallint", nullable: false),
                    SEQ = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CNT_NO_SEQ", x => x.YR);
                });

            migrationBuilder.CreateTable(
                name: "CNT_STS_LKP",
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
                    table.PrimaryKey("PK_CNT_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CNT_MAIN",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CNT_NO = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CNT_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    CRTD_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CNT_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CNT_MAIN_CNT_STS_LKP",
                        column: x => x.CNT_STS_ID,
                        principalSchema: "WHSE",
                        principalTable: "CNT_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CNT_BIN",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    BIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    OPEN_MVMT_MARK = table.Column<long>(type: "bigint", nullable: false),
                    OPEN_IND = table.Column<bool>(type: "boolean", nullable: false),
                    SUBM_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CNT_BIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CNT_BIN_BIN_MAIN",
                        column: x => x.BIN_ID,
                        principalSchema: "WHSE",
                        principalTable: "BIN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CNT_BIN_CNT_MAIN",
                        column: x => x.CNT_ID,
                        principalSchema: "WHSE",
                        principalTable: "CNT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CNT_LINE",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    BIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    VRNT_REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    GRD_ID = table.Column<short>(type: "smallint", nullable: false),
                    CNTD_QTY = table.Column<int>(type: "integer", nullable: false),
                    SYS_QTY = table.Column<int>(type: "integer", nullable: true),
                    ADJ_RSN_ID = table.Column<short>(type: "smallint", nullable: true),
                    ADJ_ID = table.Column<long>(type: "bigint", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CNT_LINE", x => x.ID);
                    table.CheckConstraint("CK_CNT_LINE_CNTD_QTY_NONNEG", "\"CNTD_QTY\" >= 0");
                    table.ForeignKey(
                        name: "FK_CNT_LINE_ADJ_MAIN",
                        column: x => x.ADJ_ID,
                        principalSchema: "WHSE",
                        principalTable: "ADJ_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CNT_LINE_ADJ_RSN_LKP",
                        column: x => x.ADJ_RSN_ID,
                        principalSchema: "WHSE",
                        principalTable: "ADJ_RSN_LKP",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_CNT_LINE_BIN_MAIN",
                        column: x => x.BIN_ID,
                        principalSchema: "WHSE",
                        principalTable: "BIN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CNT_LINE_CNT_MAIN",
                        column: x => x.CNT_ID,
                        principalSchema: "WHSE",
                        principalTable: "CNT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CNT_LINE_GRD_LKP",
                        column: x => x.GRD_ID,
                        principalSchema: "WHSE",
                        principalTable: "GRD_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CNT_LINE_VRNT_REF",
                        column: x => x.VRNT_REF_ID,
                        principalSchema: "WHSE",
                        principalTable: "VRNT_REF",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "WHSE",
                table: "CNT_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "OPEN", "Being counted", true, "Open", (short)1 },
                    { (short)2, "CLOSED", "Confirmed; variances adjusted", true, "Closed", (short)2 },
                    { (short)3, "CANCELLED", "Cancelled; no stock changed", true, "Cancelled", (short)3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CNT_BIN_BIN_ID",
                schema: "WHSE",
                table: "CNT_BIN",
                column: "BIN_ID",
                unique: true,
                filter: "\"OPEN_IND\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_CNT_BIN_CNT_ID_BIN_ID",
                schema: "WHSE",
                table: "CNT_BIN",
                columns: new[] { "CNT_ID", "BIN_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CNT_LINE_ADJ_ID",
                schema: "WHSE",
                table: "CNT_LINE",
                column: "ADJ_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CNT_LINE_ADJ_RSN_ID",
                schema: "WHSE",
                table: "CNT_LINE",
                column: "ADJ_RSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CNT_LINE_BIN_ID",
                schema: "WHSE",
                table: "CNT_LINE",
                column: "BIN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CNT_LINE_CNT_ID_BIN_ID_VRNT_REF_ID_GRD_ID",
                schema: "WHSE",
                table: "CNT_LINE",
                columns: new[] { "CNT_ID", "BIN_ID", "VRNT_REF_ID", "GRD_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CNT_LINE_GRD_ID",
                schema: "WHSE",
                table: "CNT_LINE",
                column: "GRD_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CNT_LINE_VRNT_REF_ID",
                schema: "WHSE",
                table: "CNT_LINE",
                column: "VRNT_REF_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CNT_MAIN_CNT_NO",
                schema: "WHSE",
                table: "CNT_MAIN",
                column: "CNT_NO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CNT_MAIN_CNT_STS_ID_ID",
                schema: "WHSE",
                table: "CNT_MAIN",
                columns: new[] { "CNT_STS_ID", "ID" });

            migrationBuilder.CreateIndex(
                name: "IX_CNT_STS_LKP_CODE",
                schema: "WHSE",
                table: "CNT_STS_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CNT_BIN",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "CNT_LINE",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "CNT_NO_SEQ",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "CNT_MAIN",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "CNT_STS_LKP",
                schema: "WHSE");
        }
    }
}
