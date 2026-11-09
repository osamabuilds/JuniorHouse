using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#pragma warning disable CA1861
#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Customers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsletter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NWSL_ACTN_LKP",
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
                    table.PrimaryKey("PK_NWSL_ACTN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NWSL_SRC_LKP",
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
                    table.PrimaryKey("PK_NWSL_SRC_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NWSL_STS_LKP",
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
                    table.PrimaryKey("PK_NWSL_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NWSL_SUBS",
                schema: "CUST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EML = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    CHNG_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WLCM_SENT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NWSL_SUBS", x => x.ID);
                    table.CheckConstraint("CK_NWSL_SUBS_EML_LOWR", "\"EML\" = lower(\"EML\")");
                    table.ForeignKey(
                        name: "FK_NWSL_SUBS_STS_ID",
                        column: x => x.STS_ID,
                        principalSchema: "CUST",
                        principalTable: "NWSL_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NWSL_HIST",
                schema: "CUST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SUBS_ID = table.Column<long>(type: "bigint", nullable: false),
                    ACTN_ID = table.Column<short>(type: "smallint", nullable: false),
                    SRC_ID = table.Column<short>(type: "smallint", nullable: false),
                    OCCR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NWSL_HIST", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NWSL_HIST_ACTN_ID",
                        column: x => x.ACTN_ID,
                        principalSchema: "CUST",
                        principalTable: "NWSL_ACTN_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NWSL_HIST_SRC_ID",
                        column: x => x.SRC_ID,
                        principalSchema: "CUST",
                        principalTable: "NWSL_SRC_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NWSL_HIST_SUBS_ID",
                        column: x => x.SUBS_ID,
                        principalSchema: "CUST",
                        principalTable: "NWSL_SUBS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "CUST",
                table: "NWSL_ACTN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "SUBD", null, true, "Subscribed", (short)1 },
                    { (short)2, "UNSB", null, true, "Unsubscribed", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "CUST",
                table: "NWSL_SRC_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "REGN", null, true, "Registration form", (short)1 },
                    { (short)2, "ACCT", null, true, "Account preferences", (short)2 },
                    { (short)3, "FOOT", null, true, "Footer form", (short)3 },
                    { (short)4, "LINK", null, true, "Link in an email", (short)4 }
                });

            migrationBuilder.InsertData(
                schema: "CUST",
                table: "NWSL_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "SUBD", null, true, "Subscribed", (short)1 },
                    { (short)2, "UNSB", null, true, "Unsubscribed", (short)2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_NWSL_ACTN_LKP_CODE",
                schema: "CUST",
                table: "NWSL_ACTN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NWSL_HIST_ACTN_ID",
                schema: "CUST",
                table: "NWSL_HIST",
                column: "ACTN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NWSL_HIST_SRC_ID",
                schema: "CUST",
                table: "NWSL_HIST",
                column: "SRC_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NWSL_HIST_SUBS_ID",
                schema: "CUST",
                table: "NWSL_HIST",
                column: "SUBS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NWSL_SRC_LKP_CODE",
                schema: "CUST",
                table: "NWSL_SRC_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NWSL_STS_LKP_CODE",
                schema: "CUST",
                table: "NWSL_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NWSL_SUBS_STS_ID",
                schema: "CUST",
                table: "NWSL_SUBS",
                column: "STS_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_NWSL_SUBS_EML",
                schema: "CUST",
                table: "NWSL_SUBS",
                column: "EML",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NWSL_HIST",
                schema: "CUST");

            migrationBuilder.DropTable(
                name: "NWSL_ACTN_LKP",
                schema: "CUST");

            migrationBuilder.DropTable(
                name: "NWSL_SRC_LKP",
                schema: "CUST");

            migrationBuilder.DropTable(
                name: "NWSL_SUBS",
                schema: "CUST");

            migrationBuilder.DropTable(
                name: "NWSL_STS_LKP",
                schema: "CUST");
        }
    }
}
