using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1861 // Generated: constant arrays are fine here

namespace Romp.Modules.Cart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SHPG_CITY_RATE",
                schema: "CART",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CITY_ID = table.Column<short>(type: "smallint", nullable: false),
                    FEE_AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SHPG_CITY_RATE", x => x.ID);
                    table.CheckConstraint("CK_SHPG_CITY_RATE_FEE", "\"FEE_AMT\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "SHPG_SETG",
                schema: "CART",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false),
                    DFLT_FEE_AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    FREE_THRS_AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    COD_FEE_AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SHPG_SETG", x => x.ID);
                    table.CheckConstraint("CK_SHPG_SETG_FEES", "\"DFLT_FEE_AMT\" >= 0 AND (\"COD_FEE_AMT\" IS NULL OR \"COD_FEE_AMT\" >= 0) AND (\"FREE_THRS_AMT\" IS NULL OR \"FREE_THRS_AMT\" > 0)");
                    table.CheckConstraint("CK_SHPG_SETG_ONE", "\"ID\" = 1");
                });

            migrationBuilder.InsertData(
                schema: "CART",
                table: "SHPG_SETG",
                columns: new[] { "ID", "COD_FEE_AMT", "DFLT_FEE_AMT", "FREE_THRS_AMT", "INSR_BY", "INSR_DTE", "UPDT_BY", "UPDT_DTE" },
                values: new object[] { 1L, null, 0m, null, "migration", new DateTimeOffset(new DateTime(2026, 11, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null });

            migrationBuilder.CreateIndex(
                name: "UQ_SHPG_CITY_RATE_CITY_ID",
                schema: "CART",
                table: "SHPG_CITY_RATE",
                column: "CITY_ID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SHPG_CITY_RATE",
                schema: "CART");

            migrationBuilder.DropTable(
                name: "SHPG_SETG",
                schema: "CART");
        }
    }
}
