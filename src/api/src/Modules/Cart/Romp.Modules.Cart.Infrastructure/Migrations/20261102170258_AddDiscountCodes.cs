using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1861 // Generated: constant arrays are fine here

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Cart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DISC_TYPE_LKP",
                schema: "CART",
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
                    table.PrimaryKey("PK_DISC_TYPE_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RDMP_STS_LKP",
                schema: "CART",
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
                    table.PrimaryKey("PK_RDMP_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DISC_CODE",
                schema: "CART",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CODE = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DISC_TYPE_ID = table.Column<short>(type: "smallint", nullable: false),
                    VAL = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MIN_SPND_AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    USE_CAP = table.Column<int>(type: "integer", nullable: true),
                    CUST_USE_CAP = table.Column<int>(type: "integer", nullable: true),
                    SALE_INCL_IND = table.Column<bool>(type: "boolean", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DISC_CODE", x => x.ID);
                    table.CheckConstraint("CK_DISC_CODE_LIMITS", "(\"USE_CAP\" IS NULL OR \"USE_CAP\" >= 1) AND (\"CUST_USE_CAP\" IS NULL OR \"CUST_USE_CAP\" >= 1) AND (\"MIN_SPND_AMT\" IS NULL OR \"MIN_SPND_AMT\" >= 0)");
                    table.CheckConstraint("CK_DISC_CODE_VAL", "\"VAL\" > 0 AND (\"DISC_TYPE_ID\" <> 1 OR \"VAL\" <= 100)");
                    table.ForeignKey(
                        name: "FK_DISC_CODE_DISC_TYPE_ID",
                        column: x => x.DISC_TYPE_ID,
                        principalSchema: "CART",
                        principalTable: "DISC_TYPE_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DISC_RDMP",
                schema: "CART",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DISC_CODE_ID = table.Column<long>(type: "bigint", nullable: false),
                    PLCE_KEY = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CUST_KEY = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RDMP_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    ORDR_NO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DISC_RDMP", x => x.ID);
                    table.CheckConstraint("CK_DISC_RDMP_AMT", "\"AMT\" >= 0");
                    table.ForeignKey(
                        name: "FK_DISC_RDMP_DISC_CODE_ID",
                        column: x => x.DISC_CODE_ID,
                        principalSchema: "CART",
                        principalTable: "DISC_CODE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DISC_RDMP_RDMP_STS_ID",
                        column: x => x.RDMP_STS_ID,
                        principalSchema: "CART",
                        principalTable: "RDMP_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DISC_SCOP",
                schema: "CART",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DISC_CODE_ID = table.Column<long>(type: "bigint", nullable: false),
                    CTGY_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DISC_SCOP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DISC_SCOP_DISC_CODE_ID",
                        column: x => x.DISC_CODE_ID,
                        principalSchema: "CART",
                        principalTable: "DISC_CODE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "CART",
                table: "DISC_TYPE_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "PCT", "A percentage of the eligible subtotal", true, "Percentage", (short)1 },
                    { (short)2, "FIXD", "A fixed amount in rupees", true, "Fixed amount", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "CART",
                table: "RDMP_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "HELD", "Kept for an order that is being placed", true, "Held", (short)1 },
                    { (short)2, "CNFM", "Used by a placed order", true, "Confirmed", (short)2 },
                    { (short)3, "RLSD", "Given back: the order never came or was cancelled", true, "Released", (short)3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CART_MAIN_DISC_CODE_ID",
                schema: "CART",
                table: "CART_MAIN",
                column: "DISC_CODE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_DISC_CODE_DISC_TYPE_ID",
                schema: "CART",
                table: "DISC_CODE",
                column: "DISC_TYPE_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_DISC_CODE_CODE",
                schema: "CART",
                table: "DISC_CODE",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DISC_RDMP_DISC_CODE_ID_CUST_KEY",
                schema: "CART",
                table: "DISC_RDMP",
                columns: new[] { "DISC_CODE_ID", "CUST_KEY" });

            migrationBuilder.CreateIndex(
                name: "IX_DISC_RDMP_RDMP_STS_ID_INSR_DTE",
                schema: "CART",
                table: "DISC_RDMP",
                columns: new[] { "RDMP_STS_ID", "INSR_DTE" });

            migrationBuilder.CreateIndex(
                name: "UQ_DISC_RDMP_PLCE_KEY",
                schema: "CART",
                table: "DISC_RDMP",
                column: "PLCE_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_DISC_SCOP_DISC_CODE_ID_CTGY_ID",
                schema: "CART",
                table: "DISC_SCOP",
                columns: new[] { "DISC_CODE_ID", "CTGY_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DISC_TYPE_LKP_CODE",
                schema: "CART",
                table: "DISC_TYPE_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RDMP_STS_LKP_CODE",
                schema: "CART",
                table: "RDMP_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CART_MAIN_DISC_CODE_ID",
                schema: "CART",
                table: "CART_MAIN",
                column: "DISC_CODE_ID",
                principalSchema: "CART",
                principalTable: "DISC_CODE",
                principalColumn: "ID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CART_MAIN_DISC_CODE_ID",
                schema: "CART",
                table: "CART_MAIN");

            migrationBuilder.DropTable(
                name: "DISC_RDMP",
                schema: "CART");

            migrationBuilder.DropTable(
                name: "DISC_SCOP",
                schema: "CART");

            migrationBuilder.DropTable(
                name: "RDMP_STS_LKP",
                schema: "CART");

            migrationBuilder.DropTable(
                name: "DISC_CODE",
                schema: "CART");

            migrationBuilder.DropTable(
                name: "DISC_TYPE_LKP",
                schema: "CART");

            migrationBuilder.DropIndex(
                name: "IX_CART_MAIN_DISC_CODE_ID",
                schema: "CART",
                table: "CART_MAIN");
        }
    }
}
