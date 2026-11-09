using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LGIN_OTCM_LKP",
                schema: "IDNT",
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
                name: "LGIN_PEND",
                schema: "IDNT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    USR_ID = table.Column<long>(type: "bigint", nullable: false),
                    TOKN_HASH = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FAIL_CNT = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LGIN_PEND", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LGIN_PEND_USR_ID",
                        column: x => x.USR_ID,
                        principalSchema: "IDNT",
                        principalTable: "USR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SESS_RVKD_RSN_LKP",
                schema: "IDNT",
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
                name: "LGIN_ATMP",
                schema: "IDNT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ATMP_NAME = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CLNT_IP_ADDR = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    OTCM_ID = table.Column<short>(type: "smallint", nullable: false),
                    ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    USR_ID = table.Column<long>(type: "bigint", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LGIN_ATMP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LGIN_ATMP_OTCM_ID",
                        column: x => x.OTCM_ID,
                        principalSchema: "IDNT",
                        principalTable: "LGIN_OTCM_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LGIN_ATMP_USR_ID",
                        column: x => x.USR_ID,
                        principalSchema: "IDNT",
                        principalTable: "USR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SESS_MAIN",
                schema: "IDNT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    USR_ID = table.Column<long>(type: "bigint", nullable: false),
                    ACSS_TOKN_HASH = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    RFSH_TOKN_HASH = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    PREV_RFSH_TOKN_HASH = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                    STRT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ACSS_EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RFSH_EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ABS_EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_SESS_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SESS_MAIN_RVKD_RSN_ID",
                        column: x => x.RVKD_RSN_ID,
                        principalSchema: "IDNT",
                        principalTable: "SESS_RVKD_RSN_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SESS_MAIN_USR_ID",
                        column: x => x.USR_ID,
                        principalSchema: "IDNT",
                        principalTable: "USR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "IDNT",
                table: "LGIN_OTCM_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "OK", null, true, "Password accepted", (short)1 },
                    { (short)2, "BAD_CRED", null, true, "Unknown user or wrong password", (short)2 },
                    { (short)3, "INACTIVE", null, true, "Account inactive", (short)3 },
                    { (short)4, "TEMP_EXPD", null, true, "Temporary password expired", (short)4 },
                    { (short)5, "MFA_BAD", null, true, "Wrong multi-factor code", (short)5 },
                    { (short)6, "MFA_OK", null, true, "Multi-factor code accepted", (short)6 },
                    { (short)7, "THROTTLED", null, true, "Refused while a delay was running", (short)7 },
                    { (short)8, "REFRESH_REUSE", null, true, "Rotated refresh value presented again", (short)8 }
                });

            migrationBuilder.InsertData(
                schema: "IDNT",
                table: "SESS_RVKD_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "SIGN_OUT", null, true, "Signed out", (short)1 },
                    { (short)2, "SIGN_OUT_ALL", null, true, "Signed out everywhere", (short)2 },
                    { (short)3, "REFRESH_REUSE", null, true, "Rotated refresh value presented again", (short)3 },
                    { (short)4, "NEW_SIGN_IN", null, true, "Replaced by a new sign-in", (short)4 },
                    { (short)5, "PWD_CHNG", null, true, "Password changed or reset", (short)5 },
                    { (short)6, "DEACT", null, true, "Account deactivated", (short)6 },
                    { (short)7, "ADMIN", null, true, "Revoked by an administrator", (short)7 },
                    { (short)8, "SESS_LIMIT", null, true, "Oldest session over the per-user limit", (short)8 },
                    { (short)9, "MFA_RESET", null, true, "Multi-factor enrolment reset", (short)9 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LGIN_ATMP_ATMP_DTE",
                schema: "IDNT",
                table: "LGIN_ATMP",
                column: "ATMP_DTE");

            migrationBuilder.CreateIndex(
                name: "IX_LGIN_ATMP_ATMP_NAME_ATMP_DTE",
                schema: "IDNT",
                table: "LGIN_ATMP",
                columns: new[] { "ATMP_NAME", "ATMP_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_LGIN_ATMP_ATMP_NAME_CLNT_IP_ADDR_ATMP_DTE",
                schema: "IDNT",
                table: "LGIN_ATMP",
                columns: new[] { "ATMP_NAME", "CLNT_IP_ADDR", "ATMP_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_LGIN_ATMP_OTCM_ID",
                schema: "IDNT",
                table: "LGIN_ATMP",
                column: "OTCM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_LGIN_ATMP_USR_ID",
                schema: "IDNT",
                table: "LGIN_ATMP",
                column: "USR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_LGIN_OTCM_LKP_CODE",
                schema: "IDNT",
                table: "LGIN_OTCM_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LGIN_PEND_TOKN_HASH",
                schema: "IDNT",
                table: "LGIN_PEND",
                column: "TOKN_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LGIN_PEND_USR_ID",
                schema: "IDNT",
                table: "LGIN_PEND",
                column: "USR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESS_MAIN_ABS_EXPY_DTE",
                schema: "IDNT",
                table: "SESS_MAIN",
                column: "ABS_EXPY_DTE");

            migrationBuilder.CreateIndex(
                name: "IX_SESS_MAIN_ACSS_TOKN_HASH",
                schema: "IDNT",
                table: "SESS_MAIN",
                column: "ACSS_TOKN_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SESS_MAIN_PREV_RFSH_TOKN_HASH",
                schema: "IDNT",
                table: "SESS_MAIN",
                column: "PREV_RFSH_TOKN_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SESS_MAIN_RFSH_TOKN_HASH",
                schema: "IDNT",
                table: "SESS_MAIN",
                column: "RFSH_TOKN_HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SESS_MAIN_RVKD_RSN_ID",
                schema: "IDNT",
                table: "SESS_MAIN",
                column: "RVKD_RSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESS_MAIN_USR_ID",
                schema: "IDNT",
                table: "SESS_MAIN",
                column: "USR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SESS_RVKD_RSN_LKP_CODE",
                schema: "IDNT",
                table: "SESS_RVKD_RSN_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LGIN_ATMP",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "LGIN_PEND",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "SESS_MAIN",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "LGIN_OTCM_LKP",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "SESS_RVKD_RSN_LKP",
                schema: "IDNT");
        }
    }
}
