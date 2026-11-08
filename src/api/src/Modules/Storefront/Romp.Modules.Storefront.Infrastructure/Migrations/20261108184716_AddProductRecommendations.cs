using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductRecommendations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PRDT_SALE_DAY",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    SALE_DTE = table.Column<DateOnly>(type: "date", nullable: false),
                    UNIT_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_SALE_DAY", x => x.ID);
                    table.CheckConstraint("CK_PRDT_SALE_DAY_UNIT_QTY", "\"UNIT_QTY\" >= 0");
                    table.ForeignKey(
                        name: "FK_PRDT_SALE_DAY_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "STFR",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_SMLR",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    SMLR_PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    RANK_NO = table.Column<short>(type: "smallint", nullable: false),
                    SCOR_VAL = table.Column<decimal>(type: "numeric(7,6)", precision: 7, scale: 6, nullable: false),
                    CALC_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_SMLR", x => x.ID);
                    table.CheckConstraint("CK_PRDT_SMLR_DIFF_PRDT", "\"PRDT_ID\" <> \"SMLR_PRDT_ID\"");
                    table.CheckConstraint("CK_PRDT_SMLR_RANK_NO", "\"RANK_NO\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_PRDT_SMLR_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "STFR",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PRDT_SMLR_SMLR_PRDT_ID",
                        column: x => x.SMLR_PRDT_ID,
                        principalSchema: "STFR",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RECM_ROW_LKP",
                schema: "STFR",
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
                    table.PrimaryKey("PK_RECM_ROW_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RECM_RUN",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    STRT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    END_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GRP_CNT = table.Column<int>(type: "integer", nullable: false),
                    PRDT_CNT = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECM_RUN", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RECM_EVNT_DAY",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ROW_ID = table.Column<short>(type: "smallint", nullable: false),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    EVNT_DTE = table.Column<DateOnly>(type: "date", nullable: false),
                    CLK_CNT = table.Column<int>(type: "integer", nullable: false),
                    ADD_CNT = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECM_EVNT_DAY", x => x.ID);
                    table.CheckConstraint("CK_RECM_EVNT_DAY_CNTS", "\"CLK_CNT\" >= 0 AND \"ADD_CNT\" >= 0");
                    table.ForeignKey(
                        name: "FK_RECM_EVNT_DAY_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "STFR",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RECM_EVNT_DAY_RECM_ROW_LKP",
                        column: x => x.ROW_ID,
                        principalSchema: "STFR",
                        principalTable: "RECM_ROW_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "RECM_ROW_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "SIMILAR", "Products like the one being viewed", true, "You may also like", (short)1 },
                    { (short)2, "TRENDING", "Products selling well lately", true, "Trending now", (short)2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_SALE_DAY_SALE_DTE",
                schema: "STFR",
                table: "PRDT_SALE_DAY",
                column: "SALE_DTE");

            migrationBuilder.CreateIndex(
                name: "UQ_PRDT_SALE_DAY_PRDT_ID_SALE_DTE",
                schema: "STFR",
                table: "PRDT_SALE_DAY",
                columns: new[] { "PRDT_ID", "SALE_DTE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_SMLR_SMLR_PRDT_ID",
                schema: "STFR",
                table: "PRDT_SMLR",
                column: "SMLR_PRDT_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_PRDT_SMLR_PRDT_ID_RANK_NO",
                schema: "STFR",
                table: "PRDT_SMLR",
                columns: new[] { "PRDT_ID", "RANK_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_PRDT_SMLR_PRDT_ID_SMLR_PRDT_ID",
                schema: "STFR",
                table: "PRDT_SMLR",
                columns: new[] { "PRDT_ID", "SMLR_PRDT_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RECM_EVNT_DAY_PRDT_ID",
                schema: "STFR",
                table: "RECM_EVNT_DAY",
                column: "PRDT_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_RECM_EVNT_DAY_ROW_ID_PRDT_ID_EVNT_DTE",
                schema: "STFR",
                table: "RECM_EVNT_DAY",
                columns: new[] { "ROW_ID", "PRDT_ID", "EVNT_DTE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RECM_ROW_LKP_CODE",
                schema: "STFR",
                table: "RECM_ROW_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RECM_RUN_END_DTE",
                schema: "STFR",
                table: "RECM_RUN",
                column: "END_DTE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PRDT_SALE_DAY",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PRDT_SMLR",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "RECM_EVNT_DAY",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "RECM_RUN",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "RECM_ROW_LKP",
                schema: "STFR");
        }
    }
}
