using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Reference.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "REF");

            migrationBuilder.CreateTable(
                name: "AGE_BRKT_LKP",
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
                    table.PrimaryKey("PK_AGE_BRKT_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CATG_LKP",
                schema: "REF",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRNT_CATG_ID = table.Column<short>(type: "smallint", nullable: true),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATG_LKP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CATG_LKP_PRNT_CATG_ID",
                        column: x => x.PRNT_CATG_ID,
                        principalSchema: "REF",
                        principalTable: "CATG_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CITY_LKP",
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
                    table.PrimaryKey("PK_CITY_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CLR_LKP",
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
                    table.PrimaryKey("PK_CLR_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FBRC_LKP",
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
                    table.PrimaryKey("PK_FBRC_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GNDR_LKP",
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
                    table.PrimaryKey("PK_GNDR_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PAYM_TERM_LKP",
                schema: "REF",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DFLT_ADV_PCT = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAYM_TERM_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PO_CNCL_RSN_LKP",
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
                    table.PrimaryKey("PK_PO_CNCL_RSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PO_STS_LKP",
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
                    table.PrimaryKey("PK_PO_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SIZE_LKP",
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
                    table.PrimaryKey("PK_SIZE_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "VNDR_SPCL_LKP",
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
                    table.PrimaryKey("PK_VNDR_SPCL_LKP", x => x.ID);
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "AGE_BRKT_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "NEWBORN", null, true, "Newborn (0-3M)", (short)1 },
                    { (short)2, "INFANT", null, true, "Infant (3-12M)", (short)2 },
                    { (short)3, "TODDLER", null, true, "Toddler (1-3Y)", (short)3 },
                    { (short)4, "KIDS", null, true, "Kids (4-8Y)", (short)4 },
                    { (short)5, "TWEEN", null, true, "Tween (9-12Y)", (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "CATG_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "PRNT_CATG_ID", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "TOPS", null, true, "Tops", null, (short)1 },
                    { (short)2, "BOTTOMS", null, true, "Bottoms", null, (short)2 },
                    { (short)3, "DRESSES", null, true, "Dresses", null, (short)3 },
                    { (short)4, "OUTERWEAR", null, true, "Outerwear", null, (short)4 },
                    { (short)5, "SLEEPWEAR", null, true, "Sleepwear", null, (short)5 },
                    { (short)6, "ACCESSORIES", null, true, "Accessories", null, (short)6 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "CITY_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "KHI", null, true, "Karachi", (short)1 },
                    { (short)2, "LHE", null, true, "Lahore", (short)2 },
                    { (short)3, "ISB", null, true, "Islamabad", (short)3 },
                    { (short)4, "FSD", null, true, "Faisalabad", (short)4 },
                    { (short)5, "RWP", null, true, "Rawalpindi", (short)5 },
                    { (short)6, "MUX", null, true, "Multan", (short)6 },
                    { (short)7, "SKT", null, true, "Sialkot", (short)7 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "CLR_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "WHT", null, true, "White", (short)1 },
                    { (short)2, "BLK", null, true, "Black", (short)2 },
                    { (short)3, "RED", null, true, "Red", (short)3 },
                    { (short)4, "PNK", null, true, "Pink", (short)4 },
                    { (short)5, "BLU", null, true, "Blue", (short)5 },
                    { (short)6, "NVY", null, true, "Navy", (short)6 },
                    { (short)7, "YLW", null, true, "Yellow", (short)7 },
                    { (short)8, "GRN", null, true, "Green", (short)8 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "FBRC_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "COTTON", null, true, "Cotton", (short)1 },
                    { (short)2, "DENIM", null, true, "Denim", (short)2 },
                    { (short)3, "FLEECE", null, true, "Fleece", (short)3 },
                    { (short)4, "JERSEY", null, true, "Jersey", (short)4 },
                    { (short)5, "LINEN", null, true, "Linen", (short)5 },
                    { (short)6, "POLY", null, true, "Polyester", (short)6 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "GNDR_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "BOYS", null, true, "Boys", (short)1 },
                    { (short)2, "GIRLS", null, true, "Girls", (short)2 },
                    { (short)3, "UNISEX", null, true, "Unisex", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "PAYM_TERM_LKP",
                columns: new[] { "ID", "CODE", "DFLT_ADV_PCT", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "ADV100", 100m, null, true, "100% Advance", (short)1 },
                    { (short)2, "NET50_50", 50m, null, true, "50% Advance / 50% on Delivery", (short)2 },
                    { (short)3, "NET30_70", 30m, null, true, "30% Advance / 70% on Delivery", (short)3 },
                    { (short)4, "NET30", 0m, null, true, "Net 30 (no advance)", (short)4 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "PO_CNCL_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "VendorDeclined", null, true, "Vendor Declined", (short)1 },
                    { (short)2, "CostDispute", null, true, "Cost Dispute", (short)2 },
                    { (short)3, "QualityConcern", null, true, "Quality Concern", (short)3 },
                    { (short)4, "StyleDiscontinued", null, true, "Style Discontinued", (short)4 },
                    { (short)5, "DuplicateEntry", null, true, "Duplicate Entry", (short)5 },
                    { (short)6, "Other", null, true, "Other", (short)6 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "PO_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Draft", null, true, "Draft", (short)1 },
                    { (short)2, "SentToVendor", null, true, "Sent to Vendor", (short)2 },
                    { (short)3, "Acknowledged", null, true, "Acknowledged", (short)3 },
                    { (short)4, "Cancelled", null, true, "Cancelled", (short)4 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "SIZE_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "0-3M", null, true, "0-3 Months", (short)1 },
                    { (short)2, "3-6M", null, true, "3-6 Months", (short)2 },
                    { (short)3, "6-12M", null, true, "6-12 Months", (short)3 },
                    { (short)4, "1-2Y", null, true, "1-2 Years", (short)4 },
                    { (short)5, "2-3Y", null, true, "2-3 Years", (short)5 },
                    { (short)6, "3-4Y", null, true, "3-4 Years", (short)6 },
                    { (short)7, "4-5Y", null, true, "4-5 Years", (short)7 },
                    { (short)8, "5-6Y", null, true, "5-6 Years", (short)8 },
                    { (short)9, "6-7Y", null, true, "6-7 Years", (short)9 },
                    { (short)10, "7-8Y", null, true, "7-8 Years", (short)10 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "VNDR_SPCL_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "KNITS", null, true, "Knits", (short)1 },
                    { (short)2, "WOVENS", null, true, "Wovens", (short)2 },
                    { (short)3, "UNIFORMS", null, true, "Uniforms", (short)3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AGE_BRKT_LKP_CODE",
                schema: "REF",
                table: "AGE_BRKT_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CATG_LKP_CODE",
                schema: "REF",
                table: "CATG_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CATG_LKP_PRNT_CATG_ID",
                schema: "REF",
                table: "CATG_LKP",
                column: "PRNT_CATG_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CITY_LKP_CODE",
                schema: "REF",
                table: "CITY_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CLR_LKP_CODE",
                schema: "REF",
                table: "CLR_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FBRC_LKP_CODE",
                schema: "REF",
                table: "FBRC_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GNDR_LKP_CODE",
                schema: "REF",
                table: "GNDR_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_TERM_LKP_CODE",
                schema: "REF",
                table: "PAYM_TERM_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PO_CNCL_RSN_LKP_CODE",
                schema: "REF",
                table: "PO_CNCL_RSN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PO_STS_LKP_CODE",
                schema: "REF",
                table: "PO_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SIZE_LKP_CODE",
                schema: "REF",
                table: "SIZE_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VNDR_SPCL_LKP_CODE",
                schema: "REF",
                table: "VNDR_SPCL_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AGE_BRKT_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "CATG_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "CITY_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "CLR_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "FBRC_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "GNDR_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "PAYM_TERM_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "PO_CNCL_RSN_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "PO_STS_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "SIZE_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "VNDR_SPCL_LKP",
                schema: "REF");
        }
    }
}
