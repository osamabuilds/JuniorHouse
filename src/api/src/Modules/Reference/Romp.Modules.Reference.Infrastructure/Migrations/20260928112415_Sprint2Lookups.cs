using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Reference.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2Lookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AMND_INIT_LKP",
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
                    table.PrimaryKey("PK_AMND_INIT_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "AMND_RSN_LKP",
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
                    table.PrimaryKey("PK_AMND_RSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FBRC_RESP_LKP",
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
                    table.PrimaryKey("PK_FBRC_RESP_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PO_FILE_CATG_LKP",
                schema: "REF",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VNDR_VSBL_IND = table.Column<bool>(type: "boolean", nullable: false),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PO_FILE_CATG_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PO_REV_STS_LKP",
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
                    table.PrimaryKey("PK_PO_REV_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PO_VNDR_COMM_TYP_LKP",
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
                    table.PrimaryKey("PK_PO_VNDR_COMM_TYP_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "VNDR_COMM_CHNL_LKP",
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
                    table.PrimaryKey("PK_VNDR_COMM_CHNL_LKP", x => x.ID);
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "AMND_INIT_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Buyer", null, true, "Buyer", (short)1 },
                    { (short)2, "Vendor", null, true, "Vendor", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "AMND_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "VendorCostIncrease", null, true, "Vendor Cost Increase", (short)1 },
                    { (short)2, "MoqConstraint", null, true, "MOQ Constraint", (short)2 },
                    { (short)3, "FabricOrTrimUnavailable", null, true, "Fabric or Trim Unavailable", (short)3 },
                    { (short)4, "CapacityDelay", null, true, "Capacity Delay", (short)4 },
                    { (short)5, "AdvanceRequest", null, true, "Advance Request", (short)5 },
                    { (short)6, "SizeMixChange", null, true, "Size Mix Change", (short)6 },
                    { (short)7, "ColourChange", null, true, "Colour Change", (short)7 },
                    { (short)8, "SpecChange", null, true, "Spec Change", (short)8 },
                    { (short)9, "SafetyOrCompliance", null, true, "Safety or Compliance", (short)9 },
                    { (short)10, "BuyerDemandChange", null, true, "Buyer Demand Change", (short)10 },
                    { (short)11, "Other", null, true, "Other", (short)11 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "FBRC_RESP_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "VendorSupplied", null, true, "Vendor Supplied", (short)1 },
                    { (short)2, "RompSupplied", null, true, "Romp Supplied", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "PO_FILE_CATG_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "VNDR_VSBL_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "TechPackSpec", null, true, true, "Tech Pack Spec", (short)1 },
                    { (short)2, "ArtworkLabels", null, true, true, "Artwork / Labels", (short)2 },
                    { (short)3, "TrimCardBom", null, true, true, "Trim Card / BOM", (short)3 },
                    { (short)4, "ColourStandard", null, true, true, "Colour Standard", (short)4 },
                    { (short)5, "PackingInstructions", null, true, true, "Packing Instructions", (short)5 },
                    { (short)6, "CostSheet", null, true, false, "Cost Sheet", (short)6 },
                    { (short)7, "ComplianceTestReport", null, true, false, "Compliance / Test Report", (short)7 },
                    { (short)8, "VendorEvidence", null, true, false, "Vendor Evidence", (short)8 },
                    { (short)9, "Other", null, true, false, "Other", (short)9 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "PO_REV_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Pending", null, true, "Pending", (short)1 },
                    { (short)2, "InForce", null, true, "In Force", (short)2 },
                    { (short)3, "Superseded", null, true, "Superseded", (short)3 },
                    { (short)4, "Rejected", null, true, "Rejected", (short)4 },
                    { (short)5, "Withdrawn", null, true, "Withdrawn", (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "PO_VNDR_COMM_TYP_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Confirmed", null, true, "Confirmed", (short)1 },
                    { (short)2, "Countered", null, true, "Countered", (short)2 },
                    { (short)3, "Declined", null, true, "Declined", (short)3 },
                    { (short)4, "AmendmentRequest", null, true, "Amendment Request", (short)4 },
                    { (short)5, "Decision", null, true, "Decision", (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "REF",
                table: "VNDR_COMM_CHNL_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "WhatsApp", null, true, "WhatsApp", (short)1 },
                    { (short)2, "PhoneCall", null, true, "Phone Call", (short)2 },
                    { (short)3, "Email", null, true, "Email", (short)3 },
                    { (short)4, "InPerson", null, true, "In Person", (short)4 },
                    { (short)5, "Unspecified", null, true, "Unspecified", (short)5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AMND_INIT_LKP_CODE",
                schema: "REF",
                table: "AMND_INIT_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AMND_RSN_LKP_CODE",
                schema: "REF",
                table: "AMND_RSN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FBRC_RESP_LKP_CODE",
                schema: "REF",
                table: "FBRC_RESP_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PO_FILE_CATG_LKP_CODE",
                schema: "REF",
                table: "PO_FILE_CATG_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PO_REV_STS_LKP_CODE",
                schema: "REF",
                table: "PO_REV_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PO_VNDR_COMM_TYP_LKP_CODE",
                schema: "REF",
                table: "PO_VNDR_COMM_TYP_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VNDR_COMM_CHNL_LKP_CODE",
                schema: "REF",
                table: "VNDR_COMM_CHNL_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AMND_INIT_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "AMND_RSN_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "FBRC_RESP_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "PO_FILE_CATG_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "PO_REV_STS_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "PO_VNDR_COMM_TYP_LKP",
                schema: "REF");

            migrationBuilder.DropTable(
                name: "VNDR_COMM_CHNL_LKP",
                schema: "REF");
        }
    }
}
