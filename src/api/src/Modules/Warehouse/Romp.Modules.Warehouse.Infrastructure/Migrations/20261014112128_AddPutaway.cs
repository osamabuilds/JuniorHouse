using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Warehouse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPutaway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PUT_STS_LKP",
                schema: "WHSE",
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
                    table.PrimaryKey("PK_PUT_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PUT_ITEM",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SUB_BTCH_ID = table.Column<long>(type: "bigint", nullable: false),
                    QC_BTCH_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    STYL_ID = table.Column<long>(type: "bigint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    GRD_ID = table.Column<short>(type: "smallint", nullable: false),
                    QC_QTY = table.Column<int>(type: "integer", nullable: false),
                    PUT_QTY = table.Column<int>(type: "integer", nullable: false),
                    VRNT_REF_ID = table.Column<long>(type: "bigint", nullable: true),
                    PUT_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PUT_ITEM", x => x.ID);
                    table.CheckConstraint("CK_PUT_ITEM_PUT_QTY_RANGE", "\"PUT_QTY\" >= 0 AND \"PUT_QTY\" <= \"QC_QTY\"");
                    table.CheckConstraint("CK_PUT_ITEM_QC_QTY_POS", "\"QC_QTY\" > 0");
                    table.ForeignKey(
                        name: "FK_PUT_ITEM_GRD_LKP",
                        column: x => x.GRD_ID,
                        principalSchema: "WHSE",
                        principalTable: "GRD_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PUT_ITEM_PUT_STS_LKP",
                        column: x => x.PUT_STS_ID,
                        principalSchema: "WHSE",
                        principalTable: "PUT_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PUT_ITEM_VRNT_REF",
                        column: x => x.VRNT_REF_ID,
                        principalSchema: "WHSE",
                        principalTable: "VRNT_REF",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PUT_RCRD",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PUT_ITEM_ID = table.Column<long>(type: "bigint", nullable: false),
                    BIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    SCAN_IND = table.Column<bool>(type: "boolean", nullable: false),
                    IDEM_KEY = table.Column<Guid>(type: "uuid", nullable: false),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PUT_RCRD", x => x.ID);
                    table.CheckConstraint("CK_PUT_RCRD_QTY_POS", "\"QTY\" > 0");
                    table.ForeignKey(
                        name: "FK_PUT_RCRD_BIN_MAIN",
                        column: x => x.BIN_ID,
                        principalSchema: "WHSE",
                        principalTable: "BIN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PUT_RCRD_PUT_ITEM",
                        column: x => x.PUT_ITEM_ID,
                        principalSchema: "WHSE",
                        principalTable: "PUT_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "WHSE",
                table: "PUT_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "AWAITING_GTIN", "Accepted by QC; Labelling has not assigned the barcode yet", true, "Awaiting GTIN", (short)1 },
                    { (short)2, "PENDING", "Ready to be put away", true, "Pending", (short)2 },
                    { (short)3, "COMPLETE", "All units are on the shelf", true, "Complete", (short)3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PUT_ITEM_GRD_ID",
                schema: "WHSE",
                table: "PUT_ITEM",
                column: "GRD_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PUT_ITEM_PUT_STS_ID_ID",
                schema: "WHSE",
                table: "PUT_ITEM",
                columns: new[] { "PUT_STS_ID", "ID" });

            migrationBuilder.CreateIndex(
                name: "IX_PUT_ITEM_STYL_ID_CLR_ID_SIZE_ID",
                schema: "WHSE",
                table: "PUT_ITEM",
                columns: new[] { "STYL_ID", "CLR_ID", "SIZE_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_PUT_ITEM_SUB_BTCH_ID_SIZE_ID_CLR_ID",
                schema: "WHSE",
                table: "PUT_ITEM",
                columns: new[] { "SUB_BTCH_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PUT_ITEM_VRNT_REF_ID",
                schema: "WHSE",
                table: "PUT_ITEM",
                column: "VRNT_REF_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PUT_RCRD_BIN_ID",
                schema: "WHSE",
                table: "PUT_RCRD",
                column: "BIN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PUT_RCRD_IDEM_KEY",
                schema: "WHSE",
                table: "PUT_RCRD",
                column: "IDEM_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PUT_RCRD_PUT_ITEM_ID",
                schema: "WHSE",
                table: "PUT_RCRD",
                column: "PUT_ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PUT_STS_LKP_CODE",
                schema: "WHSE",
                table: "PUT_STS_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PUT_RCRD",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "PUT_ITEM",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "PUT_STS_LKP",
                schema: "WHSE");
        }
    }
}
