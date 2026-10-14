using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Reference.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CARE_LKP",
                schema: "REF",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SYMB_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CARE_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "COLN_LKP",
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
                    table.PrimaryKey("PK_COLN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FIBR_LKP",
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
                    table.PrimaryKey("PK_FIBR_LKP", x => x.ID);
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "CARE_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ", "SYMB_CODE" },
                values: new object[,]
                {
                    { (short)1, "WASH-30", null, true, "Machine wash at 30 C", (short)1, "wash-30" },
                    { (short)2, "WASH-40", null, true, "Machine wash at 40 C", (short)2, "wash-40" },
                    { (short)3, "HAND-WASH", null, true, "Hand wash only", (short)3, "hand-wash" },
                    { (short)4, "NO-BLEACH", null, true, "Do not bleach", (short)4, "no-bleach" },
                    { (short)5, "TUMBLE-LOW", null, true, "Tumble dry low", (short)5, "tumble-low" },
                    { (short)6, "NO-TUMBLE", null, true, "Do not tumble dry", (short)6, "no-tumble" },
                    { (short)7, "IRON-LOW", null, true, "Iron on low heat", (short)7, "iron-low" },
                    { (short)8, "NO-IRON", null, true, "Do not iron", (short)8, "no-iron" },
                    { (short)9, "NO-DRYCLEAN", null, true, "Do not dry clean", (short)9, "no-dryclean" }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "COLN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "NEW-ARRIVALS", null, true, "New Arrivals", (short)1 },
                    { (short)2, "ESSENTIALS", null, true, "Essentials", (short)2 },
                    { (short)3, "SALE", null, true, "Sale", (short)3 },
                    { (short)4, "BESTSELLERS", null, true, "Bestsellers", (short)4 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "FIBR_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "COTTON", null, true, "Cotton", (short)1 },
                    { (short)2, "POLYESTER", null, true, "Polyester", (short)2 },
                    { (short)3, "ELASTANE", null, true, "Elastane", (short)3 },
                    { (short)4, "VISCOSE", null, true, "Viscose", (short)4 },
                    { (short)5, "LINEN", null, true, "Linen", (short)5 },
                    { (short)6, "WOOL", null, true, "Wool", (short)6 },
                    { (short)7, "NYLON", null, true, "Nylon", (short)7 },
                    { (short)8, "ACRYLIC", null, true, "Acrylic", (short)8 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "SIZE_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)11, "8-9Y", null, true, "8-9 Years", (short)11 },
                    { (short)12, "9-10Y", null, true, "9-10 Years", (short)12 },
                    { (short)13, "10-11Y", null, true, "10-11 Years", (short)13 },
                    { (short)14, "11-12Y", null, true, "11-12 Years", (short)14 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CARE_LKP_CODE",
                schema: "REF",
                table: "CARE_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_COLN_LKP_CODE",
                schema: "REF",
                table: "COLN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FIBR_LKP_CODE",
                schema: "REF",
                table: "FIBR_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CARE_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "COLN_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "FIBR_LKP",
                schema: "REF");

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "SIZE_LKP",
                keyColumn: "ID",
                keyValue: (short)11);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "SIZE_LKP",
                keyColumn: "ID",
                keyValue: (short)12);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "SIZE_LKP",
                keyColumn: "ID",
                keyValue: (short)13);

            migrationBuilder.DeleteData(
                schema: "REF",
                table: "SIZE_LKP",
                keyColumn: "ID",
                keyValue: (short)14);
        }
    }
}
