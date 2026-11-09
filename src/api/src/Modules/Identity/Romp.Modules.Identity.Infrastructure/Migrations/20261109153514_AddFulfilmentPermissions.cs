using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // generated migration: constant arrays are fine here

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFulfilmentPermissions : Migration
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
                    { (short)26, "fulfilment.view", "Read the pick queue, pick lists, the exception queue and packing slips", true, "View fulfilment", (short)26 },
                    { (short)27, "fulfilment.pick", "Take a pick, scan bins and units, flag a pick exception and complete the pick", true, "Pick orders", (short)27 },
                    { (short)28, "fulfilment.pack", "Open an order at the pack station, scan, weigh, complete the pack and print the slip", true, "Pack orders", (short)28 },
                    { (short)29, "fulfilment.manage", "Resolve pick exceptions and take over a pick held by someone else", true, "Manage fulfilment", (short)29 }
                });

            migrationBuilder.InsertData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                columns: new[] { "PERM_ID", "ROLE_ID" },
                values: new object[,]
                {
                    { (short)26, (short)3 },
                    { (short)27, (short)3 },
                    { (short)28, (short)3 },
                    { (short)29, (short)3 },
                    { (short)26, (short)4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)26, (short)3 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)27, (short)3 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)28, (short)3 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)29, (short)3 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)26, (short)4 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)26);

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)27);

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)28);

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)29);
        }
    }
}
