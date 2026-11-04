using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Reference.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "PROV_ID",
                schema: "REF",
                table: "CITY_LKP",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PROV_LKP",
                schema: "REF",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROV_LKP", x => x.ID);
                });

            migrationBuilder.UpdateData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)1,
                column: "PROV_ID",
                value: (short)2);

            migrationBuilder.UpdateData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)2,
                column: "PROV_ID",
                value: (short)1);

            migrationBuilder.UpdateData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)3,
                column: "PROV_ID",
                value: (short)5);

            migrationBuilder.UpdateData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)4,
                column: "PROV_ID",
                value: (short)1);

            migrationBuilder.UpdateData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)5,
                column: "PROV_ID",
                value: (short)1);

            migrationBuilder.UpdateData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)6,
                column: "PROV_ID",
                value: (short)1);

            migrationBuilder.UpdateData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)7,
                column: "PROV_ID",
                value: (short)1);

            migrationBuilder.InsertData(
                schema: "REF",
                table: "PROV_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "PB", null, true, "Punjab", (short)1 },
                    { (short)2, "SD", null, true, "Sindh", (short)2 },
                    { (short)3, "KP", null, true, "Khyber Pakhtunkhwa", (short)3 },
                    { (short)4, "BL", null, true, "Balochistan", (short)4 },
                    { (short)5, "ICT", null, true, "Islamabad Capital Territory", (short)5 },
                    { (short)6, "GB", null, true, "Gilgit-Baltistan", (short)6 },
                    { (short)7, "AJK", null, true, "Azad Jammu and Kashmir", (short)7 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "CITY_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "PROV_ID", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)8, "PEW", null, true, "Peshawar", (short)3, (short)8 },
                    { (short)9, "UET", null, true, "Quetta", (short)4, (short)9 },
                    { (short)10, "HDD", null, true, "Hyderabad", (short)2, (short)10 },
                    { (short)11, "GRW", null, true, "Gujranwala", (short)1, (short)11 },
                    { (short)12, "SGD", null, true, "Sargodha", (short)1, (short)12 },
                    { (short)13, "BWP", null, true, "Bahawalpur", (short)1, (short)13 },
                    { (short)14, "SKZ", null, true, "Sukkur", (short)2, (short)14 },
                    { (short)15, "LRK", null, true, "Larkana", (short)2, (short)15 },
                    { (short)16, "MRD", null, true, "Mardan", (short)3, (short)16 },
                    { (short)17, "ABT", null, true, "Abbottabad", (short)3, (short)17 },
                    { (short)18, "MNG", null, true, "Mingora", (short)3, (short)18 },
                    { (short)19, "KHT", null, true, "Kohat", (short)3, (short)19 },
                    { (short)20, "GIL", null, true, "Gilgit", (short)6, (short)20 },
                    { (short)21, "SKD", null, true, "Skardu", (short)6, (short)21 },
                    { (short)22, "MZG", null, true, "Muzaffarabad", (short)7, (short)22 },
                    { (short)23, "MIR", null, true, "Mirpur", (short)7, (short)23 },
                    { (short)24, "GRT", null, true, "Gujrat", (short)1, (short)24 },
                    { (short)25, "SHK", null, true, "Sheikhupura", (short)1, (short)25 },
                    { (short)26, "TRB", null, true, "Turbat", (short)4, (short)26 },
                    { (short)27, "GWD", null, true, "Gwadar", (short)4, (short)27 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CITY_LKP_PROV_ID",
                schema: "REF",
                table: "CITY_LKP",
                column: "PROV_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROV_LKP_CODE",
                schema: "REF",
                table: "PROV_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CITY_LKP_PROV_ID",
                schema: "REF",
                table: "CITY_LKP",
                column: "PROV_ID",
                principalSchema: "REF",
                principalTable: "PROV_LKP",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CITY_LKP_PROV_ID",
                schema: "REF",
                table: "CITY_LKP");

            migrationBuilder.DropTable(
                name: "PROV_LKP",
                schema: "REF");

            migrationBuilder.DropIndex(
                name: "IX_CITY_LKP_PROV_ID",
                schema: "REF",
                table: "CITY_LKP");

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)8);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)9);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)10);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)11);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)12);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)13);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)14);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)15);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)16);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)17);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)18);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)19);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)20);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)21);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)22);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)23);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)24);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)25);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)26);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "CITY_LKP",
                keyColumn: "ID",
                keyValue: (short)27);

            migrationBuilder.DropColumn(
                name: "PROV_ID",
                schema: "REF",
                table: "CITY_LKP");
        }
    }
}
