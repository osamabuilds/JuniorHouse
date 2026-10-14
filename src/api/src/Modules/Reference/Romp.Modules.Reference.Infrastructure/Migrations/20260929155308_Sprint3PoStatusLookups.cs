using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Reference.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3PoStatusLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "REF",
                table: "PO_CNCL_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[] { (short)7, "ProductionAlreadyStarted", null, true, "Cancelled after production started", (short)7 });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "PO_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)5, "InProduction", null, true, "In Production", (short)5 },
                    { (short)6, "PartiallyDelivered", null, true, "Partially Delivered", (short)6 },
                    { (short)7, "Delivered", null, true, "Delivered", (short)7 },
                    { (short)8, "Closed", null, true, "Closed", (short)8 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "REF",
                table: "PO_CNCL_RSN_LKP",
                keyColumn: "ID",
                keyValue: (short)7);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "PO_STS_LKP",
                keyColumn: "ID",
                keyValue: (short)5);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "PO_STS_LKP",
                keyColumn: "ID",
                keyValue: (short)6);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "PO_STS_LKP",
                keyColumn: "ID",
                keyValue: (short)7);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "PO_STS_LKP",
                keyColumn: "ID",
                keyValue: (short)8);
        }
    }
}
