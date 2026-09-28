using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Romp.Modules.Vendor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPoRevisionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PO_REV",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    REV_NO = table.Column<short>(type: "smallint", nullable: false),
                    INIT_ID = table.Column<short>(type: "smallint", nullable: false),
                    STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    RSN_ID = table.Column<short>(type: "smallint", nullable: false),
                    IMPC_NOTE = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    VNDR_MSG = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UNIT_COST_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    EXPC_DLVR_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    LATE_ACPT_DT = table.Column<DateOnly>(type: "date", nullable: true),
                    OVER_TOL_PCT = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    UNDR_TOL_PCT = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    PAYM_TERM_ID = table.Column<short>(type: "smallint", nullable: false),
                    ADV_PCT = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    FBRC_RESP_ID = table.Column<short>(type: "smallint", nullable: true),
                    PO_VAL_BEF_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    PO_VAL_AFT_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    PO_VAL_DIFF_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ADV_AMT_BEF = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ADV_AMT_AFT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    EXPC_DT_SHFT_DAY = table.Column<int>(type: "integer", nullable: false),
                    LATE_ACPT_DT_SHFT_DAY = table.Column<int>(type: "integer", nullable: true),
                    QTY_DIFF = table.Column<int>(type: "integer", nullable: false),
                    BYND_LATE_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_REV", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PO_REV_PO_ID",
                        column: x => x.PO_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PO_REV_STS_HIST",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_REV_ID = table.Column<long>(type: "bigint", nullable: false),
                    FROM_STS_ID = table.Column<short>(type: "smallint", nullable: true),
                    TO_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    NOTE = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_REV_STS_HIST", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PO_REV_STS_HIST_PO_REV_ID",
                        column: x => x.PO_REV_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_REV",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PO_REV_LINE",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_REV_ID = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_PO_REV_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PO_REV_LINE_PO_REV_ID",
                        column: x => x.PO_REV_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_REV",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PO_VNDR_COMM",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_REV_ID = table.Column<long>(type: "bigint", nullable: true),
                    COMM_TYP_ID = table.Column<short>(type: "smallint", nullable: false),
                    CHNL_ID = table.Column<short>(type: "smallint", nullable: false),
                    RSPR_NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RSPN_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_VNDR_COMM", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PO_VNDR_COMM_PO_ID",
                        column: x => x.PO_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PO_VNDR_COMM_PO_REV_ID",
                        column: x => x.PO_REV_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_REV",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PO_FILE",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    CATG_ID = table.Column<short>(type: "smallint", nullable: false),
                    FILE_NAME = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    STOR_KEY = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CNTT_TYP = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FILE_SIZE_BYT = table.Column<long>(type: "bigint", nullable: false),
                    ADDD_REV_ID = table.Column<long>(type: "bigint", nullable: true),
                    RETD_REV_ID = table.Column<long>(type: "bigint", nullable: true),
                    DELD_IND = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    VNDR_COMM_ID = table.Column<long>(type: "bigint", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_FILE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PO_FILE_PO_ID",
                        column: x => x.PO_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PO_FILE_ADDD_REV_ID",
                        column: x => x.ADDD_REV_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_REV",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PO_FILE_RETD_REV_ID",
                        column: x => x.RETD_REV_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_REV",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PO_FILE_VNDR_COMM_ID",
                        column: x => x.VNDR_COMM_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_VNDR_COMM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PO_REV_PO_ID_REV_NO",
                schema: "VNDR",
                table: "PO_REV",
                columns: new[] { "PO_ID", "REV_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PO_REV_PO_ID_PEND",
                schema: "VNDR",
                table: "PO_REV",
                column: "PO_ID",
                unique: true,
                filter: "\"STS_ID\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PO_REV_INIT_ID",
                schema: "VNDR",
                table: "PO_REV",
                column: "INIT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_REV_STS_ID",
                schema: "VNDR",
                table: "PO_REV",
                column: "STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_REV_RSN_ID",
                schema: "VNDR",
                table: "PO_REV",
                column: "RSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_REV_PAYM_TERM_ID",
                schema: "VNDR",
                table: "PO_REV",
                column: "PAYM_TERM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_REV_STS_HIST_PO_REV_ID",
                schema: "VNDR",
                table: "PO_REV_STS_HIST",
                column: "PO_REV_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_REV_LINE_PO_REV_ID_SIZE_ID_CLR_ID",
                schema: "VNDR",
                table: "PO_REV_LINE",
                columns: new[] { "PO_REV_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PO_VNDR_COMM_PO_ID",
                schema: "VNDR",
                table: "PO_VNDR_COMM",
                column: "PO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_VNDR_COMM_PO_REV_ID",
                schema: "VNDR",
                table: "PO_VNDR_COMM",
                column: "PO_REV_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_VNDR_COMM_COMM_TYP_ID",
                schema: "VNDR",
                table: "PO_VNDR_COMM",
                column: "COMM_TYP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_VNDR_COMM_CHNL_ID",
                schema: "VNDR",
                table: "PO_VNDR_COMM",
                column: "CHNL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_FILE_PO_ID",
                schema: "VNDR",
                table: "PO_FILE",
                column: "PO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_FILE_CATG_ID",
                schema: "VNDR",
                table: "PO_FILE",
                column: "CATG_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_FILE_ADDD_REV_ID",
                schema: "VNDR",
                table: "PO_FILE",
                column: "ADDD_REV_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_FILE_RETD_REV_ID",
                schema: "VNDR",
                table: "PO_FILE",
                column: "RETD_REV_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_FILE_VNDR_COMM_ID",
                schema: "VNDR",
                table: "PO_FILE",
                column: "VNDR_COMM_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PO_FILE",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "PO_REV_LINE",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "PO_REV_STS_HIST",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "PO_VNDR_COMM",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "PO_REV",
                schema: "VNDR");
        }
    }
}
