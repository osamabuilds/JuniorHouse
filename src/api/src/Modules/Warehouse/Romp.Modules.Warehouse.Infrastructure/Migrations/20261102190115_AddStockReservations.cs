using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

namespace Romp.Modules.Warehouse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RSRV_STS_LKP",
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
                    table.PrimaryKey("PK_RSRV_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "STCK_RSRV_BAL",
                schema: "WHSE",
                columns: table => new
                {
                    VRNT_REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    RSRV_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STCK_RSRV_BAL", x => x.VRNT_REF_ID);
                    table.CheckConstraint("CK_STCK_RSRV_BAL_RSRV_QTY", "\"RSRV_QTY\" >= 0");
                    table.ForeignKey(
                        name: "FK_STCK_RSRV_BAL_VRNT_REF",
                        column: x => x.VRNT_REF_ID,
                        principalSchema: "WHSE",
                        principalTable: "VRNT_REF",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "STCK_RSRV",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PLCE_KEY = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RSRV_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    HELD_UNTL_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ORDR_NO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STCK_RSRV", x => x.ID);
                    table.ForeignKey(
                        name: "FK_STCK_RSRV_RSRV_STS_LKP",
                        column: x => x.RSRV_STS_ID,
                        principalSchema: "WHSE",
                        principalTable: "RSRV_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "STCK_RSRV_LINE",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RSRV_ID = table.Column<long>(type: "bigint", nullable: false),
                    VRNT_REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STCK_RSRV_LINE", x => x.ID);
                    table.CheckConstraint("CK_STCK_RSRV_LINE_QTY", "\"QTY\" > 0");
                    table.ForeignKey(
                        name: "FK_STCK_RSRV_LINE_STCK_RSRV",
                        column: x => x.RSRV_ID,
                        principalSchema: "WHSE",
                        principalTable: "STCK_RSRV",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_STCK_RSRV_LINE_VRNT_REF",
                        column: x => x.VRNT_REF_ID,
                        principalSchema: "WHSE",
                        principalTable: "VRNT_REF",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "WHSE",
                table: "RSRV_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "HELD", "Stock set aside while the order is being placed", true, "Held", (short)1 },
                    { (short)2, "CONFIRMED", "The order exists; the units are promised to it", true, "Confirmed", (short)2 },
                    { (short)3, "CONVERTED", "Picked: the ledger has been decremented", true, "Converted", (short)3 },
                    { (short)4, "RELEASED", "Given back: the order was cancelled or never came", true, "Released", (short)4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RSRV_STS_LKP_CODE",
                schema: "WHSE",
                table: "RSRV_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_STCK_RSRV_RSRV_STS_ID_HELD_UNTL_DTE",
                schema: "WHSE",
                table: "STCK_RSRV",
                columns: new[] { "RSRV_STS_ID", "HELD_UNTL_DTE" });

            migrationBuilder.CreateIndex(
                name: "UQ_STCK_RSRV_PLCE_KEY",
                schema: "WHSE",
                table: "STCK_RSRV",
                column: "PLCE_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_STCK_RSRV_LINE_VRNT_REF_ID",
                schema: "WHSE",
                table: "STCK_RSRV_LINE",
                column: "VRNT_REF_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_STCK_RSRV_LINE_RSRV_ID_VRNT_REF_ID",
                schema: "WHSE",
                table: "STCK_RSRV_LINE",
                columns: new[] { "RSRV_ID", "VRNT_REF_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "STCK_RSRV_BAL",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "STCK_RSRV_LINE",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "STCK_RSRV",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "RSRV_STS_LKP",
                schema: "WHSE");
        }
    }
}
