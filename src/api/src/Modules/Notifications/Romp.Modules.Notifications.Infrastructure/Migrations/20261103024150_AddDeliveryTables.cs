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
    public partial class AddDeliveryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CHNL_LKP",
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
                    table.PrimaryKey("PK_CHNL_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CHNL_STS_LKP",
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
                    table.PrimaryKey("PK_CHNL_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ERR_CATG_LKP",
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
                    table.PrimaryKey("PK_ERR_CATG_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NTFY_KIND_LKP",
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
                    table.PrimaryKey("PK_NTFY_KIND_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "REF_KIND_LKP",
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
                    table.PrimaryKey("PK_REF_KIND_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NTFY_DAY_CNT",
                schema: "NTFY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DAY_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    CHNL_ID = table.Column<short>(type: "smallint", nullable: false),
                    SENT_CNT = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NTFY_DAY_CNT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NTFY_DAY_CNT_CHNL_LKP",
                        column: x => x.CHNL_ID,
                        principalSchema: "NTFY",
                        principalTable: "CHNL_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NTFY_TMPL",
                schema: "NTFY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NTFY_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    CHNL_ID = table.Column<short>(type: "smallint", nullable: false),
                    LANG = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    VER = table.Column<short>(type: "smallint", nullable: false),
                    SUBJ = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TEXT_BODY = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    HTML_BODY = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    PRVD_TMPL_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NTFY_TMPL", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NTFY_TMPL_CHNL_LKP",
                        column: x => x.CHNL_ID,
                        principalSchema: "NTFY",
                        principalTable: "CHNL_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NTFY_TMPL_NTFY_KIND_LKP",
                        column: x => x.NTFY_KIND_ID,
                        principalSchema: "NTFY",
                        principalTable: "NTFY_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NTFY_MAIN",
                schema: "NTFY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NTFY_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    REF_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    EVNT_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PRIO = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NTFY_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NTFY_MAIN_NTFY_KIND_LKP",
                        column: x => x.NTFY_KIND_ID,
                        principalSchema: "NTFY",
                        principalTable: "NTFY_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NTFY_MAIN_REF_KIND_LKP",
                        column: x => x.REF_KIND_ID,
                        principalSchema: "NTFY",
                        principalTable: "REF_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NTFY_CHNL",
                schema: "NTFY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NTFY_ID = table.Column<long>(type: "bigint", nullable: false),
                    CHNL_ID = table.Column<short>(type: "smallint", nullable: false),
                    CHNL_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    ATMP_CNT = table.Column<int>(type: "integer", nullable: false),
                    NXT_ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LEAS_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ERR_CATG_ID = table.Column<short>(type: "smallint", nullable: true),
                    PRVD_MSG_ID = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FLBK_OF_ID = table.Column<long>(type: "bigint", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NTFY_CHNL", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NTFY_CHNL_CHNL_LKP",
                        column: x => x.CHNL_ID,
                        principalSchema: "NTFY",
                        principalTable: "CHNL_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NTFY_CHNL_CHNL_STS_LKP",
                        column: x => x.CHNL_STS_ID,
                        principalSchema: "NTFY",
                        principalTable: "CHNL_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NTFY_CHNL_ERR_CATG_LKP",
                        column: x => x.ERR_CATG_ID,
                        principalSchema: "NTFY",
                        principalTable: "ERR_CATG_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NTFY_CHNL_FLBK_OF",
                        column: x => x.FLBK_OF_ID,
                        principalSchema: "NTFY",
                        principalTable: "NTFY_CHNL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NTFY_CHNL_NTFY_MAIN",
                        column: x => x.NTFY_ID,
                        principalSchema: "NTFY",
                        principalTable: "NTFY_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NTFY_ATMP",
                schema: "NTFY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NTFY_CHNL_ID = table.Column<long>(type: "bigint", nullable: false),
                    ATMP_NO = table.Column<int>(type: "integer", nullable: false),
                    CHNL_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    ERR_CATG_ID = table.Column<short>(type: "smallint", nullable: true),
                    ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NTFY_ATMP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NTFY_ATMP_CHNL_STS_LKP",
                        column: x => x.CHNL_STS_ID,
                        principalSchema: "NTFY",
                        principalTable: "CHNL_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NTFY_ATMP_ERR_CATG_LKP",
                        column: x => x.ERR_CATG_ID,
                        principalSchema: "NTFY",
                        principalTable: "ERR_CATG_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NTFY_ATMP_NTFY_CHNL",
                        column: x => x.NTFY_CHNL_ID,
                        principalSchema: "NTFY",
                        principalTable: "NTFY_CHNL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NTFY_DEDL",
                schema: "NTFY",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NTFY_CHNL_ID = table.Column<long>(type: "bigint", nullable: false),
                    NTFY_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    CHNL_ID = table.Column<short>(type: "smallint", nullable: false),
                    ERR_CATG_ID = table.Column<short>(type: "smallint", nullable: false),
                    ATMP_CNT = table.Column<int>(type: "integer", nullable: false),
                    DEDL_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RSND_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RSND_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NTFY_DEDL", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NTFY_DEDL_CHNL_LKP",
                        column: x => x.CHNL_ID,
                        principalSchema: "NTFY",
                        principalTable: "CHNL_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NTFY_DEDL_ERR_CATG_LKP",
                        column: x => x.ERR_CATG_ID,
                        principalSchema: "NTFY",
                        principalTable: "ERR_CATG_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NTFY_DEDL_NTFY_CHNL",
                        column: x => x.NTFY_CHNL_ID,
                        principalSchema: "NTFY",
                        principalTable: "NTFY_CHNL",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NTFY_DEDL_NTFY_KIND_LKP",
                        column: x => x.NTFY_KIND_ID,
                        principalSchema: "NTFY",
                        principalTable: "NTFY_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "CHNL_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "EMAL", "Email through the SMTP sender", true, "Email", (short)1 },
                    { (short)2, "WHTS", "WhatsApp, only for customers who opted in", true, "WhatsApp", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "CHNL_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "QUED", "Waiting to be sent or to be retried", true, "Queued", (short)1 },
                    { (short)2, "SENT", "Handed to the provider", true, "Sent", (short)2 },
                    { (short)3, "DLVD", "The provider confirmed it reached the customer", true, "Delivered", (short)3 },
                    { (short)4, "FAIL", "A permanent error; not retried", true, "Failed", (short)4 },
                    { (short)5, "DEAD", "Every attempt failed; in the dead-letter table", true, "Dead", (short)5 },
                    { (short)6, "EXPR", "Too late to be useful, so it was not sent", true, "Expired", (short)6 },
                    { (short)7, "SKIP", "Not sent on this channel: no opt-in, stopped or no address", true, "Skipped", (short)7 }
                });

            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "ERR_CATG_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "TMPY", "The provider or network failed; worth retrying", true, "Temporary", (short)1 },
                    { (short)2, "PERM", "The address or number is invalid; retrying will not help", true, "Permanent", (short)2 },
                    { (short)3, "CIRC", "The channel is paused after repeated failures", true, "Circuit open", (short)3 },
                    { (short)4, "OPTO", "No opt-in, or the number said STOP", true, "Opted out", (short)4 },
                    { (short)5, "NOAD", "There is no email address or number to send to", true, "No address", (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "NTFY_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "ORCF", "The order was received (cash on delivery) or paid (online)", true, "Order confirmation", (short)1 },
                    { (short)2, "ORCN", "The order was cancelled; says why in a category", true, "Order cancelled", (short)2 },
                    { (short)3, "CODR", "Asks the customer to confirm or cancel a cash on delivery order", true, "Cash on delivery confirmation request", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "REF_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[] { (short)1, "ORDR", "The notification is about an order", true, "Order", (short)1 });

            migrationBuilder.CreateIndex(
                name: "IX_CHNL_LKP_CODE",
                schema: "NTFY",
                table: "CHNL_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CHNL_STS_LKP_CODE",
                schema: "NTFY",
                table: "CHNL_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ERR_CATG_LKP_CODE",
                schema: "NTFY",
                table: "ERR_CATG_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_ATMP_CHNL_STS_ID",
                schema: "NTFY",
                table: "NTFY_ATMP",
                column: "CHNL_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_ATMP_ERR_CATG_ID",
                schema: "NTFY",
                table: "NTFY_ATMP",
                column: "ERR_CATG_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_ATMP_NTFY_CHNL_ID_ATMP_NO",
                schema: "NTFY",
                table: "NTFY_ATMP",
                columns: new[] { "NTFY_CHNL_ID", "ATMP_NO" });

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_CHNL_CHNL_ID",
                schema: "NTFY",
                table: "NTFY_CHNL",
                column: "CHNL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_CHNL_CHNL_STS_ID",
                schema: "NTFY",
                table: "NTFY_CHNL",
                column: "CHNL_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_CHNL_ERR_CATG_ID",
                schema: "NTFY",
                table: "NTFY_CHNL",
                column: "ERR_CATG_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_CHNL_FLBK_OF_ID",
                schema: "NTFY",
                table: "NTFY_CHNL",
                column: "FLBK_OF_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_CHNL_NTFY_ID",
                schema: "NTFY",
                table: "NTFY_CHNL",
                column: "NTFY_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_CHNL_NXT_ATMP_DTE_DUE",
                schema: "NTFY",
                table: "NTFY_CHNL",
                column: "NXT_ATMP_DTE",
                filter: "\"CHNL_STS_ID\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_CHNL_PRVD_MSG_ID",
                schema: "NTFY",
                table: "NTFY_CHNL",
                column: "PRVD_MSG_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_NTFY_CHNL_NTFY_ID_CHNL_ID",
                schema: "NTFY",
                table: "NTFY_CHNL",
                columns: new[] { "NTFY_ID", "CHNL_ID" },
                unique: true,
                filter: "\"FLBK_OF_ID\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_DAY_CNT_CHNL_ID",
                schema: "NTFY",
                table: "NTFY_DAY_CNT",
                column: "CHNL_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_NTFY_DAY_CNT_DAY_DT_CHNL_ID",
                schema: "NTFY",
                table: "NTFY_DAY_CNT",
                columns: new[] { "DAY_DT", "CHNL_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_DEDL_CHNL_ID",
                schema: "NTFY",
                table: "NTFY_DEDL",
                column: "CHNL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_DEDL_DEDL_DTE",
                schema: "NTFY",
                table: "NTFY_DEDL",
                column: "DEDL_DTE");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_DEDL_ERR_CATG_ID",
                schema: "NTFY",
                table: "NTFY_DEDL",
                column: "ERR_CATG_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_DEDL_NTFY_KIND_ID",
                schema: "NTFY",
                table: "NTFY_DEDL",
                column: "NTFY_KIND_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_NTFY_DEDL_NTFY_CHNL_ID",
                schema: "NTFY",
                table: "NTFY_DEDL",
                column: "NTFY_CHNL_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_KIND_LKP_CODE",
                schema: "NTFY",
                table: "NTFY_KIND_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_MAIN_NTFY_KIND_ID",
                schema: "NTFY",
                table: "NTFY_MAIN",
                column: "NTFY_KIND_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_MAIN_REF_KIND_ID_REF_ID",
                schema: "NTFY",
                table: "NTFY_MAIN",
                columns: new[] { "REF_KIND_ID", "REF_ID" });

            migrationBuilder.CreateIndex(
                name: "UQ_NTFY_MAIN_EVNT_ID_NTFY_KIND_ID",
                schema: "NTFY",
                table: "NTFY_MAIN",
                columns: new[] { "EVNT_ID", "NTFY_KIND_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NTFY_TMPL_CHNL_ID",
                schema: "NTFY",
                table: "NTFY_TMPL",
                column: "CHNL_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_NTFY_TMPL_KIND_CHNL_LANG_VER",
                schema: "NTFY",
                table: "NTFY_TMPL",
                columns: new[] { "NTFY_KIND_ID", "CHNL_ID", "LANG", "VER" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REF_KIND_LKP_CODE",
                schema: "NTFY",
                table: "REF_KIND_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NTFY_ATMP",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "NTFY_DAY_CNT",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "NTFY_DEDL",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "NTFY_TMPL",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "NTFY_CHNL",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "CHNL_LKP",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "CHNL_STS_LKP",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "ERR_CATG_LKP",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "NTFY_MAIN",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "NTFY_KIND_LKP",
                schema: "NTFY");

            migrationBuilder.DropTable(
                name: "REF_KIND_LKP",
                schema: "NTFY");
        }
    }
}
