using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // generated migration: constant arrays are fine here

namespace Romp.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDelayedKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "NTFY_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[] { (short)4, "ORDL", "A line of the order is waiting for stock; the customer is told it will ship later", true, "Order delayed", (short)4 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "NTFY",
                table: "NTFY_KIND_LKP",
                keyColumn: "ID",
                keyValue: (short)4);
        }
    }
}
