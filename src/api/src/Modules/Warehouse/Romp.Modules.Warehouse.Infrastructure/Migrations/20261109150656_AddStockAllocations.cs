using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

namespace Romp.Modules.Warehouse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RVW_RSN",
                schema: "WHSE",
                table: "BIN_MAIN",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ALOC_STS_LKP",
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
                    table.PrimaryKey("PK_ALOC_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "STCK_ALOC",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_KEY = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ALOC_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STCK_ALOC", x => x.ID);
                    table.ForeignKey(
                        name: "FK_STCK_ALOC_ALOC_STS_LKP",
                        column: x => x.ALOC_STS_ID,
                        principalSchema: "WHSE",
                        principalTable: "ALOC_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "STCK_ALOC_LINE",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ALOC_ID = table.Column<long>(type: "bigint", nullable: false),
                    VRNT_REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    GRD_ID = table.Column<short>(type: "smallint", nullable: false),
                    BIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STCK_ALOC_LINE", x => x.ID);
                    table.CheckConstraint("CK_STCK_ALOC_LINE_QTY", "\"QTY\" > 0");
                    table.ForeignKey(
                        name: "FK_STCK_ALOC_LINE_BIN_MAIN",
                        column: x => x.BIN_ID,
                        principalSchema: "WHSE",
                        principalTable: "BIN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_STCK_ALOC_LINE_STCK_ALOC",
                        column: x => x.ALOC_ID,
                        principalSchema: "WHSE",
                        principalTable: "STCK_ALOC",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_STCK_ALOC_LINE_VRNT_REF",
                        column: x => x.VRNT_REF_ID,
                        principalSchema: "WHSE",
                        principalTable: "VRNT_REF",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "WHSE",
                table: "ALOC_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "ACTIVE", "Units are set aside in bins for the order", true, "Active", (short)1 },
                    { (short)2, "PICKED", "The pick was confirmed and the ledger decremented", true, "Picked", (short)2 },
                    { (short)3, "RELEASED", "Given back: the order was cancelled", true, "Released", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "WHSE",
                table: "MVMT_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[] { (short)3, "PICK", "Picked for an order: the reservation became a decrement", true, "Pick", (short)3 });

            migrationBuilder.CreateIndex(
                name: "IX_ALOC_STS_LKP_CODE",
                schema: "WHSE",
                table: "ALOC_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_STCK_ALOC_ALOC_STS_ID",
                schema: "WHSE",
                table: "STCK_ALOC",
                column: "ALOC_STS_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_STCK_ALOC_ORDR_KEY",
                schema: "WHSE",
                table: "STCK_ALOC",
                column: "ORDR_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_STCK_ALOC_LINE_BIN_ID",
                schema: "WHSE",
                table: "STCK_ALOC_LINE",
                column: "BIN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STCK_ALOC_LINE_VRNT_REF_ID_GRD_ID_BIN_ID",
                schema: "WHSE",
                table: "STCK_ALOC_LINE",
                columns: new[] { "VRNT_REF_ID", "GRD_ID", "BIN_ID" });

            migrationBuilder.CreateIndex(
                name: "UQ_STCK_ALOC_LINE_ALOC_VRNT_GRD_BIN",
                schema: "WHSE",
                table: "STCK_ALOC_LINE",
                columns: new[] { "ALOC_ID", "VRNT_REF_ID", "GRD_ID", "BIN_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "STCK_ALOC_LINE",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "STCK_ALOC",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "ALOC_STS_LKP",
                schema: "WHSE");

            migrationBuilder.DeleteData(
                schema: "WHSE",
                table: "MVMT_KIND_LKP",
                keyColumn: "ID",
                keyValue: (short)3);

            migrationBuilder.DropColumn(
                name: "RVW_RSN",
                schema: "WHSE",
                table: "BIN_MAIN");
        }
    }
}
