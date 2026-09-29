using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Romp.Modules.Vendor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPoCommercialTermsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LATE_ACPT_DT",
                schema: "VNDR",
                table: "PO_MAIN",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OVER_TOL_PCT",
                schema: "VNDR",
                table: "PO_MAIN",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UNDR_TOL_PCT",
                schema: "VNDR",
                table: "PO_MAIN",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "FBRC_RESP_ID",
                schema: "VNDR",
                table: "PO_MAIN",
                type: "smallint",
                nullable: true);

            // Cross-schema lookup reference (REF.FBRC_RESP_LKP) - index only, no FK constraint
            // across schemas (ADR 0002), same treatment as PAYM_TERM_ID's existing index.
            migrationBuilder.CreateIndex(
                name: "IX_PO_MAIN_FBRC_RESP_ID",
                schema: "VNDR",
                table: "PO_MAIN",
                column: "FBRC_RESP_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PO_MAIN_FBRC_RESP_ID",
                schema: "VNDR",
                table: "PO_MAIN");

            migrationBuilder.DropColumn(
                name: "LATE_ACPT_DT",
                schema: "VNDR",
                table: "PO_MAIN");

            migrationBuilder.DropColumn(
                name: "OVER_TOL_PCT",
                schema: "VNDR",
                table: "PO_MAIN");

            migrationBuilder.DropColumn(
                name: "UNDR_TOL_PCT",
                schema: "VNDR",
                table: "PO_MAIN");

            migrationBuilder.DropColumn(
                name: "FBRC_RESP_ID",
                schema: "VNDR",
                table: "PO_MAIN");
        }
    }
}
