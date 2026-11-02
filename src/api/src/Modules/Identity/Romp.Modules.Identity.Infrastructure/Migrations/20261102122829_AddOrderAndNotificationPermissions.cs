using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Generated seed data: constant arrays are fine here

namespace Romp.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAndNotificationPermissions : Migration
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
                    { (short)20, "orders.view", "Read the order queue, order details and COD flags", true, "View orders", (short)20 },
                    { (short)21, "orders.manage", "Record COD outcomes, cancel orders and clear COD flags", true, "Manage orders", (short)21 },
                    { (short)22, "discounts.manage", "Create, change and deactivate discount codes", true, "Manage discount codes", (short)22 },
                    { (short)23, "shipping.manage", "Set the shipping fee per city, the default fee and the free-shipping threshold", true, "Manage shipping rates", (short)23 },
                    { (short)24, "notifications.view", "Read the delivery log and dead letters", true, "View notifications", (short)24 },
                    { (short)25, "notifications.resend", "Resend a notification from the delivery log", true, "Resend notifications", (short)25 }
                });

            migrationBuilder.InsertData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                columns: new[] { "PERM_ID", "ROLE_ID" },
                values: new object[,]
                {
                    { (short)20, (short)3 },
                    { (short)21, (short)3 },
                    { (short)24, (short)3 },
                    { (short)25, (short)3 },
                    { (short)20, (short)4 },
                    { (short)24, (short)4 },
                    { (short)25, (short)4 },
                    { (short)22, (short)5 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)23);

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)20, (short)3 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)21, (short)3 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)24, (short)3 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)25, (short)3 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)20, (short)4 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)24, (short)4 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)25, (short)4 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                keyColumns: new[] { "PERM_ID", "ROLE_ID" },
                keyValues: new object[] { (short)22, (short)5 });

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)20);

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)21);

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)22);

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)24);

            migrationBuilder.DeleteData(
                schema: "IDNT",
                table: "PERM_LKP",
                keyColumn: "ID",
                keyValue: (short)25);
        }
    }
}
