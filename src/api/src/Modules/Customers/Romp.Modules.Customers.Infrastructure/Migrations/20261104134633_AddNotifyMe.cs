using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#pragma warning disable CA1861
#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Customers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifyMe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NTFM_STS_LKP",
                schema: "CUST",
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
                    table.PrimaryKey("PK_NTFM_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NTFM_REQ",
                schema: "CUST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VRNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    EML = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    NTFD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SENT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NTFM_REQ", x => x.ID);
                    table.CheckConstraint("CK_NTFM_REQ_EML_LOWR", "\"EML\" = lower(\"EML\")");
                    table.ForeignKey(
                        name: "FK_NTFM_REQ_STS_ID",
                        column: x => x.STS_ID,
                        principalSchema: "CUST",
                        principalTable: "NTFM_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "CUST",
                table: "NTFM_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "OPEN", null, true, "Open", (short)1 },
                    { (short)2, "NTFD", null, true, "Notified", (short)2 },
                    { (short)3, "EXPD", null, true, "Expired", (short)3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_NTFM_REQ_STS_ID",
                schema: "CUST",
                table: "NTFM_REQ",
                column: "STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFM_REQ_VRNT_ID_STS_ID",
                schema: "CUST",
                table: "NTFM_REQ",
                columns: new[] { "VRNT_ID", "STS_ID" });

            migrationBuilder.CreateIndex(
                name: "UQ_NTFM_REQ_VRNT_ID_EML_OPEN",
                schema: "CUST",
                table: "NTFM_REQ",
                columns: new[] { "VRNT_ID", "EML" },
                unique: true,
                filter: "\"STS_ID\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_NTFM_STS_LKP_CODE",
                schema: "CUST",
                table: "NTFM_STS_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NTFM_REQ",
                schema: "CUST");

            migrationBuilder.DropTable(
                name: "NTFM_STS_LKP",
                schema: "CUST");
        }
    }
}
