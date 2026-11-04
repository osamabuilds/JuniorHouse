using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#pragma warning disable CA1861
#nullable disable

#pragma warning disable CA1814

namespace Romp.Modules.Customers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CUST_WSHL",
                schema: "CUST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CUST_ID = table.Column<long>(type: "bigint", nullable: false),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    SAVD_PRIC_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUST_WSHL", x => x.ID);
                    table.CheckConstraint("CK_CUST_WSHL_SAVD_PRIC_AMT", "\"SAVD_PRIC_AMT\" >= 0");
                    table.ForeignKey(
                        name: "FK_CUST_WSHL_CUST_ID",
                        column: x => x.CUST_ID,
                        principalSchema: "CUST",
                        principalTable: "CUST_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CUST_WSHL_CUST_ID",
                schema: "CUST",
                table: "CUST_WSHL",
                column: "CUST_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_CUST_WSHL_CUST_ID_PRDT_ID",
                schema: "CUST",
                table: "CUST_WSHL",
                columns: new[] { "CUST_ID", "PRDT_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CUST_WSHL",
                schema: "CUST");
        }
    }
}
