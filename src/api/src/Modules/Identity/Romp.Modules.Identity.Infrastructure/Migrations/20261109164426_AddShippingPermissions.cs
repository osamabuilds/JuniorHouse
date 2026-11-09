using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814
#pragma warning disable CA1861 // generated migration: constant arrays are fine here

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "IDNT",
                table: "PERM_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)30, "shipping.view", "Read shipments, the manual booking queue, labels and manifests", true, "View shipping", (short)30 },
                    { (short)31, "shipping.ops", "Retry or hand-enter a booking, print labels, make, count and resolve manifests", true, "Operate shipping", (short)31 }
                });

            migrationBuilder.InsertData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                columns: new[] { "PERM_ID", "ROLE_ID" },
                values: new object[,]
                {
                    { (short)30, (short)3 },
                    { (short)31, (short)3 },
                    { (short)30, (short)4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)30, (short)3 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)31, (short)3 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)30, (short)4 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)30);

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)31);
        }
    }
}
