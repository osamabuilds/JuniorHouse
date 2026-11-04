using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Romp.Modules.Customers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CUST_ADDR",
                schema: "CUST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CUST_ID = table.Column<long>(type: "bigint", nullable: false),
                    RCPT_NAME = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PHON = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PROV_ID = table.Column<short>(type: "smallint", nullable: false),
                    CITY_ID = table.Column<short>(type: "smallint", nullable: false),
                    LINE_ONE = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LINE_TWO = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PCDE = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DFLT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUST_ADDR", x => x.ID);
                    table.CheckConstraint("CK_CUST_ADDR_LINE", "char_length(btrim(\"LINE_ONE\")) BETWEEN 5 AND 150 AND (\"LINE_TWO\" IS NULL OR char_length(btrim(\"LINE_TWO\")) BETWEEN 1 AND 150)");
                    table.CheckConstraint("CK_CUST_ADDR_PCDE", "\"PCDE\" IS NULL OR \"PCDE\" ~ '^[A-Za-z0-9]{1,10}$'");
                    table.CheckConstraint("CK_CUST_ADDR_PHON", "\"PHON\" ~ '^\\+92[2-9][0-9]{9}$'");
                    table.CheckConstraint("CK_CUST_ADDR_RCPT", "char_length(btrim(\"RCPT_NAME\")) BETWEEN 2 AND 80");
                    table.ForeignKey(
                        name: "FK_CUST_ADDR_CUST_ID",
                        column: x => x.CUST_ID,
                        principalSchema: "CUST",
                        principalTable: "CUST_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CUST_ADDR_CUST_ID",
                schema: "CUST",
                table: "CUST_ADDR",
                column: "CUST_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_CUST_ADDR_DFLT",
                schema: "CUST",
                table: "CUST_ADDR",
                column: "CUST_ID",
                unique: true,
                filter: "\"DFLT_IND\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CUST_ADDR",
                schema: "CUST");
        }
    }
}
