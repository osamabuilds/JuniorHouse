using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Romp.Modules.Vendor.Domain.Vendors;

#nullable disable

namespace Romp.Modules.Vendor.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDispatcherColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AGGR_ID",
                schema: "VNDR",
                table: "OUTB_MSG",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AGGR_TYP",
                schema: "VNDR",
                table: "OUTB_MSG",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "PurchaseOrder");

            migrationBuilder.AddColumn<short>(
                name: "ATMP_CNT",
                schema: "VNDR",
                table: "OUTB_MSG",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "CLM_BY",
                schema: "VNDR",
                table: "OUTB_MSG",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DEDL_IND",
                schema: "VNDR",
                table: "OUTB_MSG",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DEDL_RSN",
                schema: "VNDR",
                table: "OUTB_MSG",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LEAS_EXPY_DTE",
                schema: "VNDR",
                table: "OUTB_MSG",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "MSG_VER",
                schema: "VNDR",
                table: "OUTB_MSG",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NXT_ATMP_DTE",
                schema: "VNDR",
                table: "OUTB_MSG",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PROC_DTE",
                schema: "VNDR",
                table: "OUTB_MSG",
                type: "timestamp with time zone",
                nullable: true);

            // Data migration (plan.md's "backfilled from PYLD where possible"): every Sprint 1
            // event (PoCreatedEvent/PoSentToVendorEvent/PoAcknowledgedEvent/PoCancelledEvent)
            // serializes its PurchaseOrder aggregate id as JSON property "PoId" - rows whose
            // payload predates that shape (there are none in production, but defensively) stay
            // null rather than fail the migration.
            migrationBuilder.Sql(
                """UPDATE "VNDR"."OUTB_MSG" SET "AGGR_ID" = ("PYLD"->>'PoId')::bigint WHERE "PYLD" ? 'PoId'""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AGGR_ID",
                schema: "VNDR",
                table: "OUTB_MSG");

            migrationBuilder.DropColumn(
                name: "AGGR_TYP",
                schema: "VNDR",
                table: "OUTB_MSG");

            migrationBuilder.DropColumn(
                name: "ATMP_CNT",
                schema: "VNDR",
                table: "OUTB_MSG");

            migrationBuilder.DropColumn(
                name: "CLM_BY",
                schema: "VNDR",
                table: "OUTB_MSG");

            migrationBuilder.DropColumn(
                name: "DEDL_IND",
                schema: "VNDR",
                table: "OUTB_MSG");

            migrationBuilder.DropColumn(
                name: "DEDL_RSN",
                schema: "VNDR",
                table: "OUTB_MSG");

            migrationBuilder.DropColumn(
                name: "LEAS_EXPY_DTE",
                schema: "VNDR",
                table: "OUTB_MSG");

            migrationBuilder.DropColumn(
                name: "MSG_VER",
                schema: "VNDR",
                table: "OUTB_MSG");

            migrationBuilder.DropColumn(
                name: "NXT_ATMP_DTE",
                schema: "VNDR",
                table: "OUTB_MSG");

            migrationBuilder.DropColumn(
                name: "PROC_DTE",
                schema: "VNDR",
                table: "OUTB_MSG");
        }
    }
}
