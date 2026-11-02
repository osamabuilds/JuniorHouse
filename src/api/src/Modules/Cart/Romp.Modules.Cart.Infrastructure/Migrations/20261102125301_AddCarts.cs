using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1861 // Generated: constant arrays are fine here

namespace Romp.Modules.Cart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCarts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "CART");

            migrationBuilder.CreateTable(
                name: "CART_MAIN",
                schema: "CART",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CUST_ID = table.Column<long>(type: "bigint", nullable: true),
                    GST_KEY_HASH = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DISC_CODE_ID = table.Column<long>(type: "bigint", nullable: true),
                    LAST_USED_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CART_MAIN", x => x.ID);
                    table.CheckConstraint("CK_CART_MAIN_OWNR", "(\"CUST_ID\" IS NULL) <> (\"GST_KEY_HASH\" IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "CART",
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
                schema: "CART",
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
                name: "CART_LINE",
                schema: "CART",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CART_ID = table.Column<long>(type: "bigint", nullable: false),
                    VRNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CART_LINE", x => x.ID);
                    table.CheckConstraint("CK_CART_LINE_QTY", "\"QTY\" > 0");
                    table.ForeignKey(
                        name: "FK_CART_LINE_CART_ID",
                        column: x => x.CART_ID,
                        principalSchema: "CART",
                        principalTable: "CART_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_CART_LINE_CART_ID_VRNT_ID",
                schema: "CART",
                table: "CART_LINE",
                columns: new[] { "CART_ID", "VRNT_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CART_MAIN_EXPY_DTE",
                schema: "CART",
                table: "CART_MAIN",
                column: "EXPY_DTE",
                filter: "\"EXPY_DTE\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_CART_MAIN_CUST_ID",
                schema: "CART",
                table: "CART_MAIN",
                column: "CUST_ID",
                unique: true,
                filter: "\"CUST_ID\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_CART_MAIN_GST_KEY_HASH",
                schema: "CART",
                table: "CART_MAIN",
                column: "GST_KEY_HASH",
                unique: true,
                filter: "\"GST_KEY_HASH\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CART_LINE",
                schema: "CART");

            migrationBuilder.DropTable(
                name: "INBX",
                schema: "CART");

            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "CART");

            migrationBuilder.DropTable(
                name: "CART_MAIN",
                schema: "CART");
        }
    }
}
