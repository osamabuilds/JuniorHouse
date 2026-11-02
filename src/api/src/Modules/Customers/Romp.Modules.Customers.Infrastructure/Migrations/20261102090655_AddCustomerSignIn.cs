using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#pragma warning disable CA1861
#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Customers.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerSignIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EML_VRFY_DTE",
                schema: "CUST",
                table: "CUST_MAIN",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LAST_LGIN_DTE",
                schema: "CUST",
                table: "CUST_MAIN",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PWD_HASH",
                schema: "CUST",
                table: "CUST_MAIN",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CUST_EML_VRFY",
                schema: "CUST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EML = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CUST_ID = table.Column<long>(type: "bigint", nullable: false),
                    TOKN_HASH = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    ISSD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    USED_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUST_EML_VRFY", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CUST_EML_VRFY_CUST_ID",
                        column: x => x.CUST_ID,
                        principalSchema: "CUST",
                        principalTable: "CUST_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CUST_PWD_RSET",
                schema: "CUST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CUST_ID = table.Column<long>(type: "bigint", nullable: false),
                    TOKN_HASH = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    ISSD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    USED_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUST_PWD_RSET", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CUST_PWD_RSET_CUST_ID",
                        column: x => x.CUST_ID,
                        principalSchema: "CUST",
                        principalTable: "CUST_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LGIN_OTCM_LKP",
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
                    table.PrimaryKey("PK_LGIN_OTCM_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SESS_RVKD_RSN_LKP",
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
                    table.PrimaryKey("PK_SESS_RVKD_RSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CUST_LGIN_ATMP",
                schema: "CUST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ATMP_EML = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CLNT_IP_ADDR = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    OTCM_ID = table.Column<short>(type: "smallint", nullable: false),
                    ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CUST_ID = table.Column<long>(type: "bigint", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUST_LGIN_ATMP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CUST_LGIN_ATMP_CUST_ID",
                        column: x => x.CUST_ID,
                        principalSchema: "CUST",
                        principalTable: "CUST_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CUST_LGIN_ATMP_OTCM_ID",
                        column: x => x.OTCM_ID,
                        principalSchema: "CUST",
                        principalTable: "LGIN_OTCM_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CUST_SESS",
                schema: "CUST",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CUST_ID = table.Column<long>(type: "bigint", nullable: false),
                    ACSS_TOKN_HASH = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    RFSH_TOKN_HASH = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    PREV_RFSH_TOKN_HASH = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                    KEEP_IND = table.Column<bool>(type: "boolean", nullable: false),
                    STRT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ACSS_EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RFSH_EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ABS_EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ROTD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RVKD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RVKD_RSN_ID = table.Column<short>(type: "smallint", nullable: true),
                    CLNT_IP_ADDR = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    USR_AGNT = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CUST_SESS", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CUST_SESS_CUST_ID",
                        column: x => x.CUST_ID,
                        principalSchema: "CUST",
                        principalTable: "CUST_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CUST_SESS_RVKD_RSN_ID",
                        column: x => x.RVKD_RSN_ID,
                        principalSchema: "CUST",
                        principalTable: "SESS_RVKD_RSN_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "CUST",
                table: "LGIN_OTCM_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "OK", null, true, "Password accepted", (short)1 },
                    { (short)2, "BAD_CRED", null, true, "Unknown email or wrong password", (short)2 },
                    { (short)3, "THROTTLED", null, true, "Refused by the throttle", (short)3 },
                    { (short)4, "CAPTCHA", null, true, "CAPTCHA missing or failed", (short)4 },
                    { (short)5, "REFRESH_REUSE", null, true, "Rotated refresh value presented again", (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "CUST",
                table: "SESS_RVKD_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "SIGN_OUT", null, true, "Signed out", (short)1 },
                    { (short)2, "SIGN_OUT_ALL", null, true, "Signed out everywhere", (short)2 },
                    { (short)3, "REFRESH_REUSE", null, true, "Rotated refresh value presented again", (short)3 },
                    { (short)4, "NEW_SIGN_IN", null, true, "Replaced by a new sign-in", (short)4 },
                    { (short)5, "PWD_CHNG", null, true, "Password changed or reset", (short)5 },
                    { (short)6, "SESS_LIMIT", null, true, "Oldest session over the per-customer limit", (short)6 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CUST_MAIN_PHON",
                schema: "CUST",
                table: "CUST_MAIN",
                column: "PHON");

            migrationBuilder.CreateIndex(
                name: "IX_CUST_EML_VRFY_CUST_ID",
                schema: "CUST",
                table: "CUST_EML_VRFY",
                column: "CUST_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CUST_EML_VRFY_EXPY_DTE",
                schema: "CUST",
                table: "CUST_EML_VRFY",
                column: "EXPY_DTE");

            migrationBuilder.CreateIndex(
                name: "IX_CUST_EML_VRFY_TOKN_HASH",
                schema: "CUST",
                table: "CUST_EML_VRFY",
                column: "TOKN_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CUST_LGIN_ATMP_ATMP_DTE",
                schema: "CUST",
                table: "CUST_LGIN_ATMP",
                column: "ATMP_DTE");

            migrationBuilder.CreateIndex(
                name: "IX_CUST_LGIN_ATMP_ATMP_EML_ATMP_DTE",
                schema: "CUST",
                table: "CUST_LGIN_ATMP",
                columns: new[] { "ATMP_EML", "ATMP_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_CUST_LGIN_ATMP_ATMP_EML_CLNT_IP_ADDR_ATMP_DTE",
                schema: "CUST",
                table: "CUST_LGIN_ATMP",
                columns: new[] { "ATMP_EML", "CLNT_IP_ADDR", "ATMP_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_CUST_LGIN_ATMP_CUST_ID",
                schema: "CUST",
                table: "CUST_LGIN_ATMP",
                column: "CUST_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CUST_LGIN_ATMP_OTCM_ID",
                schema: "CUST",
                table: "CUST_LGIN_ATMP",
                column: "OTCM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CUST_PWD_RSET_CUST_ID",
                schema: "CUST",
                table: "CUST_PWD_RSET",
                column: "CUST_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CUST_PWD_RSET_EXPY_DTE",
                schema: "CUST",
                table: "CUST_PWD_RSET",
                column: "EXPY_DTE");

            migrationBuilder.CreateIndex(
                name: "IX_CUST_PWD_RSET_TOKN_HASH",
                schema: "CUST",
                table: "CUST_PWD_RSET",
                column: "TOKN_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CUST_SESS_ABS_EXPY_DTE",
                schema: "CUST",
                table: "CUST_SESS",
                column: "ABS_EXPY_DTE");

            migrationBuilder.CreateIndex(
                name: "IX_CUST_SESS_ACSS_TOKN_HASH",
                schema: "CUST",
                table: "CUST_SESS",
                column: "ACSS_TOKN_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CUST_SESS_CUST_ID",
                schema: "CUST",
                table: "CUST_SESS",
                column: "CUST_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CUST_SESS_PREV_RFSH_TOKN_HASH",
                schema: "CUST",
                table: "CUST_SESS",
                column: "PREV_RFSH_TOKN_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CUST_SESS_RFSH_TOKN_HASH",
                schema: "CUST",
                table: "CUST_SESS",
                column: "RFSH_TOKN_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CUST_SESS_RVKD_RSN_ID",
                schema: "CUST",
                table: "CUST_SESS",
                column: "RVKD_RSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_LGIN_OTCM_LKP_CODE",
                schema: "CUST",
                table: "LGIN_OTCM_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SESS_RVKD_RSN_LKP_CODE",
                schema: "CUST",
                table: "SESS_RVKD_RSN_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CUST_EML_VRFY",
                schema: "CUST");

            migrationBuilder.DropTable(
                name: "CUST_LGIN_ATMP",
                schema: "CUST");

            migrationBuilder.DropTable(
                name: "CUST_PWD_RSET",
                schema: "CUST");

            migrationBuilder.DropTable(
                name: "CUST_SESS",
                schema: "CUST");

            migrationBuilder.DropTable(
                name: "LGIN_OTCM_LKP",
                schema: "CUST");

            migrationBuilder.DropTable(
                name: "SESS_RVKD_RSN_LKP",
                schema: "CUST");

            migrationBuilder.DropIndex(
                name: "IX_CUST_MAIN_PHON",
                schema: "CUST",
                table: "CUST_MAIN");

            migrationBuilder.DropColumn(
                name: "EML_VRFY_DTE",
                schema: "CUST",
                table: "CUST_MAIN");

            migrationBuilder.DropColumn(
                name: "LAST_LGIN_DTE",
                schema: "CUST",
                table: "CUST_MAIN");

            migrationBuilder.DropColumn(
                name: "PWD_HASH",
                schema: "CUST",
                table: "CUST_MAIN");
        }
    }
}
