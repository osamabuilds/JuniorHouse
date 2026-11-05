using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

namespace Romp.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLookupAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LOOK_ATMP",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ADDR_HASH = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FAIL_IND = table.Column<bool>(type: "boolean", nullable: false),
                    ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOOK_ATMP", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LOOK_ATMP_ADDR_HASH_ATMP_DTE",
                schema: "ORDR",
                table: "LOOK_ATMP",
                columns: new[] { "ADDR_HASH", "ATMP_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_LOOK_ATMP_ATMP_DTE",
                schema: "ORDR",
                table: "LOOK_ATMP",
                column: "ATMP_DTE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LOOK_ATMP",
                schema: "ORDR");
        }
    }
}
