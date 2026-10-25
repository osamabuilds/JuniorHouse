using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "IDNT");

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "IDNT",
                columns: table => new
                {
                    MSG_ID = table.Column<long>(type: "bigint", nullable: false),
                    HNDL_NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PROC_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INBX", x => new { x.MSG_ID, x.HNDL_NAME });
                });

            migrationBuilder.CreateTable(
                name: "OUTB_MSG",
                schema: "IDNT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EVNT_TYP = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PYLD = table.Column<string>(type: "jsonb", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AGGR_TYP = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PurchaseOrder"),
                    AGGR_ID = table.Column<long>(type: "bigint", nullable: true),
                    MSG_VER = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    ATMP_CNT = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    NXT_ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CLM_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LEAS_EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PROC_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DEDL_IND = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DEDL_RSN = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OUTB_MSG", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PERM_LKP",
                schema: "IDNT",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PERM_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ROLE_LKP",
                schema: "IDNT",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    ALL_PERM_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROLE_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ROLE_PERM_MAP",
                schema: "IDNT",
                columns: table => new
                {
                    ROLE_ID = table.Column<short>(type: "smallint", nullable: false),
                    PERM_ID = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROLE_PERM_MAP", x => new { x.ROLE_ID, x.PERM_ID });
                    table.ForeignKey(
                        name: "FK_ROLE_PERM_MAP_PERM_LKP_PERM_ID",
                        column: x => x.PERM_ID,
                        principalSchema: "IDNT",
                        principalTable: "PERM_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ROLE_PERM_MAP_ROLE_LKP_ROLE_ID",
                        column: x => x.ROLE_ID,
                        principalSchema: "IDNT",
                        principalTable: "ROLE_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "IDNT",
                table: "PERM_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "ref.read", "Read the reference lookups", true, "View reference data", (short)1 },
                    { (short)2, "ref.write", "Change the reference lookups", true, "Change reference data", (short)2 },
                    { (short)3, "catalog.read", "Read styles, products, media, size charts and URLs", true, "View the catalogue", (short)3 },
                    { (short)4, "catalog.write", "Change styles, products, media, composition and publish", true, "Change the catalogue", (short)4 },
                    { (short)5, "procurement.read", "Read vendors, purchase orders, revisions and files", true, "View procurement", (short)5 },
                    { (short)6, "procurement.write", "Change vendors, purchase orders, revisions and files", true, "Change procurement", (short)6 },
                    { (short)7, "production.read", "Read runs, milestones, samples and deliveries", true, "View production", (short)7 },
                    { (short)8, "production.write", "Change runs, milestones, samples and deliveries", true, "Change production", (short)8 },
                    { (short)9, "qc.read", "Read inbound deliveries, GRNs, batches and inspections", true, "View quality control", (short)9 },
                    { (short)10, "qc.write", "Record inspections and accept or reject batches", true, "Change quality control", (short)10 },
                    { (short)11, "labelling.read", "Read work items, variants and print jobs", true, "View labelling", (short)11 },
                    { (short)12, "labelling.write", "Assign barcodes and print labels", true, "Change labelling", (short)12 },
                    { (short)13, "warehouse.read", "Read bins and putaway", true, "View the warehouse", (short)13 },
                    { (short)14, "warehouse.write", "Change bins and confirm putaway", true, "Change the warehouse", (short)14 },
                    { (short)15, "inventory.read", "Read stock levels, the ledger, adjustments, counts and alerts", true, "View inventory", (short)15 },
                    { (short)16, "inventory.write", "Post adjustments, approve cycle counts and set alert thresholds", true, "Change inventory", (short)16 },
                    { (short)17, "ops.admin", "Outbox and operational data, voids", true, "Operate the system", (short)17 },
                    { (short)18, "staff.manage", "Staff accounts, roles and MFA reset", true, "Manage staff", (short)18 },
                    { (short)19, "audit.read", "Read the audit trail", true, "View the audit trail", (short)19 }
                });

            migrationBuilder.InsertData(
                schema: "IDNT",
                table: "ROLE_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ALL_PERM_IND", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "OWNER", "Holds every permission, including ones added later", true, true, "Owner", (short)1 },
                    { (short)2, "CATALOGUE", "Maintains styles, products, media and publishing", false, true, "Catalogue", (short)2 },
                    { (short)3, "ORDER_OPS", "Views stock and the catalogue to dispatch orders; cannot write stock", false, true, "Order operations", (short)3 },
                    { (short)4, "SUPPORT", "Customer support; order permissions arrive with the order sprints", false, true, "Support", (short)4 },
                    { (short)5, "MARKETING", "Marketing; promotion permissions arrive with the promotions sprint", false, true, "Marketing", (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                columns: new[] { "PERM_ID", "ROLE_ID" },
                values: new object[,]
                {
                    { (short)1, (short)2 },
                    { (short)3, (short)2 },
                    { (short)4, (short)2 },
                    { (short)13, (short)2 },
                    { (short)1, (short)3 },
                    { (short)3, (short)3 },
                    { (short)13, (short)3 },
                    { (short)15, (short)3 },
                    { (short)1, (short)4 },
                    { (short)3, (short)4 },
                    { (short)1, (short)5 },
                    { (short)3, (short)5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PERM_LKP_CODE",
                schema: "IDNT",
                table: "PERM_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ROLE_LKP_CODE",
                schema: "IDNT",
                table: "ROLE_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ROLE_PERM_MAP_PERM_ID",
                schema: "IDNT",
                table: "ROLE_PERM_MAP",
                column: "PERM_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INBX",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "ROLE_PERM_MAP",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "PERM_LKP",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "ROLE_LKP",
                schema: "IDNT");
        }
    }
}
