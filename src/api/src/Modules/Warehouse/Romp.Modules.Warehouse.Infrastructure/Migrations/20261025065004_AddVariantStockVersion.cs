using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Romp.Modules.Warehouse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantStockVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "STCK_VER",
                schema: "WHSE",
                table: "VRNT_REF",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // A variant that already has ledger rows must not read as "never had stock" (PCT-9): start it at its number of movements,
            // which is at least one and only has to be below every version a later posting assigns (each adds one).
            migrationBuilder.Sql(
                """
                UPDATE "WHSE"."VRNT_REF" v
                SET "STCK_VER" = m."Cnt"
                FROM (SELECT "VRNT_REF_ID", COUNT(*) AS "Cnt" FROM "WHSE"."STCK_MVMT" GROUP BY "VRNT_REF_ID") m
                WHERE m."VRNT_REF_ID" = v."ID";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "STCK_VER",
                schema: "WHSE",
                table: "VRNT_REF");
        }
    }
}
