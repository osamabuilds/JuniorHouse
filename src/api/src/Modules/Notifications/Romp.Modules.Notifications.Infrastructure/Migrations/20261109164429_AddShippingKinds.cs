using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814
#pragma warning disable CA1861 // generated migration: constant arrays are fine here

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "NTFY_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)5, "ORSH", "The courier collected the parcel; carries the tracking link", true, "Order shipped", (short)5 },
                    { (short)6, "DAFL", "The courier could not deliver; the customer can act before the parcel goes back", true, "Delivery attempt failed", (short)6 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "NTFY",
                table: "NTFY_KIND_LKP",
                keyColumn: "ID",
                keyValue: (short)5);

            migrationBuilder.DeleteData(
                schema: "NTFY",
                table: "NTFY_KIND_LKP",
                keyColumn: "ID",
                keyValue: (short)6);
        }
    }
}
