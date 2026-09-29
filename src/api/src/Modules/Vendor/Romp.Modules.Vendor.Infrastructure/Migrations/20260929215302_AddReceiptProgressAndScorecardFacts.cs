#pragma warning disable CA1861
﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Romp.Modules.Vendor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptProgressAndScorecardFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PO_RCVD",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    GRN_ID = table.Column<long>(type: "bigint", nullable: false),
                    GRN_NO = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RCVD_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    CLM_TOT_QTY = table.Column<int>(type: "integer", nullable: false),
                    CNTD_TOT_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_RCVD", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PO_RCVD_PO_ID",
                        column: x => x.PO_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SCRD_DLVR",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VNDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    VNDR_DN_NO = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DSPT_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    ONTM_IND = table.Column<bool>(type: "boolean", nullable: false),
                    ONQT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    ROMP_FBRC_DLAY_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCRD_DLVR", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SCRD_DLVR_PO_ID",
                        column: x => x.PO_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SCRD_DLVR_VNDR_ID",
                        column: x => x.VNDR_ID,
                        principalSchema: "VNDR",
                        principalTable: "VNDR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SCRD_INSP",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VNDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    BTCH_ID = table.Column<long>(type: "bigint", nullable: false),
                    INSP_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    INSP_QTY = table.Column<int>(type: "integer", nullable: false),
                    DFCT_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SCRD_INSP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SCRD_INSP_PO_ID",
                        column: x => x.PO_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SCRD_INSP_VNDR_ID",
                        column: x => x.VNDR_ID,
                        principalSchema: "VNDR",
                        principalTable: "VNDR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PO_RCVD_LINE",
                schema: "VNDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_RCVD_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLM_QTY = table.Column<int>(type: "integer", nullable: false),
                    CNTD_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_RCVD_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PO_RCVD_LINE_PO_RCVD_ID",
                        column: x => x.PO_RCVD_ID,
                        principalSchema: "VNDR",
                        principalTable: "PO_RCVD",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PO_RCVD_GRN_ID",
                schema: "VNDR",
                table: "PO_RCVD",
                column: "GRN_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PO_RCVD_PO_ID",
                schema: "VNDR",
                table: "PO_RCVD",
                column: "PO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PO_RCVD_LINE_PO_RCVD_ID_SIZE_ID_CLR_ID",
                schema: "VNDR",
                table: "PO_RCVD_LINE",
                columns: new[] { "PO_RCVD_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SCRD_DLVR_PO_ID_VNDR_DN_NO",
                schema: "VNDR",
                table: "SCRD_DLVR",
                columns: new[] { "PO_ID", "VNDR_DN_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SCRD_DLVR_VNDR_ID",
                schema: "VNDR",
                table: "SCRD_DLVR",
                column: "VNDR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SCRD_INSP_BTCH_ID",
                schema: "VNDR",
                table: "SCRD_INSP",
                column: "BTCH_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SCRD_INSP_PO_ID",
                schema: "VNDR",
                table: "SCRD_INSP",
                column: "PO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SCRD_INSP_VNDR_ID",
                schema: "VNDR",
                table: "SCRD_INSP",
                column: "VNDR_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PO_RCVD_LINE",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "SCRD_DLVR",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "SCRD_INSP",
                schema: "VNDR");

            migrationBuilder.DropTable(
                name: "PO_RCVD",
                schema: "VNDR");
        }
    }
}
