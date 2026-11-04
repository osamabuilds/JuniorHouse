using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // generated migration: constant arrays are fine

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppInbound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NTFY_BLCK",
                schema: "NTFY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NO_HASH = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BLCK_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NTFY_BLCK", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "WBHK_TYP_LKP",
                schema: "NTFY",
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
                    table.PrimaryKey("PK_WBHK_TYP_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NTFY_WBHK",
                schema: "NTFY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRVD_EVNT_ID = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WBHK_TYP_ID = table.Column<short>(type: "smallint", nullable: true),
                    SGNT_VRFY_IND = table.Column<bool>(type: "boolean", nullable: false),
                    BODY_HASH = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NTFY_CHNL_ID = table.Column<long>(type: "bigint", nullable: true),
                    RCVD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PROC_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NTFY_WBHK", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NTFY_WBHK_NTFY_CHNL",
                        column: x => x.NTFY_CHNL_ID,
                        principalSchema: "NTFY",
                        principalTable: "NTFY_CHNL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NTFY_WBHK_WBHK_TYP_LKP",
                        column: x => x.WBHK_TYP_ID,
                        principalSchema: "NTFY",
                        principalTable: "WBHK_TYP_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "WBHK_TYP_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "BTNR", "The customer pressed a quick-reply button (Confirm or Cancel)", true, "Button reply", (short)1 },
                    { (short)2, "STOP", "The customer asked to stop WhatsApp messages", true, "Stop", (short)2 },
                    { (short)3, "RCPT", "A delivery receipt from the provider", true, "Receipt", (short)3 }
                });

            migrationBuilder.CreateIndex(
                name: "UQ_NTFY_BLCK_NO_HASH",
                schema: "NTFY",
                table: "NTFY_BLCK",
                column: "NO_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_WBHK_NTFY_CHNL_ID",
                schema: "NTFY",
                table: "NTFY_WBHK",
                column: "NTFY_CHNL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_WBHK_WBHK_TYP_ID",
                schema: "NTFY",
                table: "NTFY_WBHK",
                column: "WBHK_TYP_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_NTFY_WBHK_PRVD_EVNT_ID",
                schema: "NTFY",
                table: "NTFY_WBHK",
                column: "PRVD_EVNT_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WBHK_TYP_LKP_CODE",
                schema: "NTFY",
                table: "WBHK_TYP_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NTFY_BLCK",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "NTFY_WBHK",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "WBHK_TYP_LKP",
                schema: "NTFY");
        }
    }
}
