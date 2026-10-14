using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CHRT_MAIN",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CATG_ID = table.Column<short>(type: "smallint", nullable: false),
                    GNDR_ID = table.Column<short>(type: "smallint", nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CHRT_MAIN", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "CTLG",
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
                schema: "CTLG",
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
                name: "PRDT_STS_LKP",
                schema: "CTLG",
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
                    table.PrimaryKey("PK_PRDT_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "VRNT_LBL",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LBL_VRNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    STYL_ID = table.Column<long>(type: "bigint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    SKU_CODE = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    GTIN = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    GTIN_VOID_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VRNT_LBL", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CHRT_LINE",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CHRT_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    AGE_MIN_MON = table.Column<int>(type: "integer", nullable: true),
                    AGE_MAX_MON = table.Column<int>(type: "integer", nullable: true),
                    HGHT_MIN_CM = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    HGHT_MAX_CM = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    WGHT_MIN_KG = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    WGHT_MAX_KG = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    CHST_CM = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    WAIS_CM = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    LEN_CM = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CHRT_LINE", x => x.ID);
                    table.CheckConstraint("CK_CHRT_LINE_POSITIVE", "COALESCE(\"AGE_MIN_MON\", 0) >= 0 AND COALESCE(\"AGE_MAX_MON\", 0) >= 0 AND COALESCE(\"HGHT_MIN_CM\", 0) >= 0 AND COALESCE(\"HGHT_MAX_CM\", 0) >= 0 AND COALESCE(\"WGHT_MIN_KG\", 0) >= 0 AND COALESCE(\"WGHT_MAX_KG\", 0) >= 0 AND COALESCE(\"CHST_CM\", 0) >= 0 AND COALESCE(\"WAIS_CM\", 0) >= 0 AND COALESCE(\"LEN_CM\", 0) >= 0");
                    table.CheckConstraint("CK_CHRT_LINE_RANGE", "(\"AGE_MIN_MON\" IS NULL OR \"AGE_MAX_MON\" IS NULL OR \"AGE_MIN_MON\" <= \"AGE_MAX_MON\") AND (\"HGHT_MIN_CM\" IS NULL OR \"HGHT_MAX_CM\" IS NULL OR \"HGHT_MIN_CM\" <= \"HGHT_MAX_CM\") AND (\"WGHT_MIN_KG\" IS NULL OR \"WGHT_MAX_KG\" IS NULL OR \"WGHT_MIN_KG\" <= \"WGHT_MAX_KG\")");
                    table.ForeignKey(
                        name: "FK_CHRT_LINE_CHRT_ID",
                        column: x => x.CHRT_ID,
                        principalSchema: "CTLG",
                        principalTable: "CHRT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_MAIN",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    STYL_ID = table.Column<long>(type: "bigint", nullable: false),
                    NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    GNDR_ID = table.Column<short>(type: "smallint", nullable: false),
                    CATG_ID = table.Column<short>(type: "smallint", nullable: false),
                    AGE_BRKT_ID = table.Column<short>(type: "smallint", nullable: false),
                    PRDT_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    SLUG = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CARE_NOTE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PUBL_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PUBL_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_MAIN", x => x.ID);
                    table.CheckConstraint("CK_PRDT_MAIN_SLUG", "\"SLUG\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "FK_PRDT_MAIN_PRDT_STS_ID",
                        column: x => x.PRDT_STS_ID,
                        principalSchema: "CTLG",
                        principalTable: "PRDT_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PRDT_MAIN_STYL_ID",
                        column: x => x.STYL_ID,
                        principalSchema: "CTLG",
                        principalTable: "STYL_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_CARE_MAP",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    CARE_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_CARE_MAP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PRDT_CARE_MAP_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "CTLG",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_CMPS",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    FIBR_ID = table.Column<short>(type: "smallint", nullable: false),
                    PCT = table.Column<decimal>(type: "numeric(4,1)", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_CMPS", x => x.ID);
                    table.CheckConstraint("CK_PRDT_CMPS_PCT", "\"PCT\" > 0 AND \"PCT\" <= 100");
                    table.ForeignKey(
                        name: "FK_PRDT_CMPS_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "CTLG",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_COLN_MAP",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    COLN_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_COLN_MAP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PRDT_COLN_MAP_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "CTLG",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_IMG",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: true),
                    FILE_KEY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CNTT_TYP = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FILE_NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FILE_BYT = table.Column<int>(type: "integer", nullable: false),
                    WDTH = table.Column<int>(type: "integer", nullable: false),
                    HGHT = table.Column<int>(type: "integer", nullable: false),
                    SORT_SEQ = table.Column<int>(type: "integer", nullable: false),
                    ALT = table.Column<string>(type: "character varying(125)", maxLength: 125, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_IMG", x => x.ID);
                    table.CheckConstraint("CK_PRDT_IMG_ALT", "\"ALT\" IS NULL OR char_length(btrim(\"ALT\")) BETWEEN 1 AND 125");
                    table.CheckConstraint("CK_PRDT_IMG_SIZE", "\"WDTH\" > 0 AND \"HGHT\" > 0 AND \"FILE_BYT\" > 0");
                    table.ForeignKey(
                        name: "FK_PRDT_IMG_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "CTLG",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_SLUG_HIST",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    GNDR_ID = table.Column<short>(type: "smallint", nullable: false),
                    CATG_ID = table.Column<short>(type: "smallint", nullable: false),
                    SLUG = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_SLUG_HIST", x => x.ID);
                    table.CheckConstraint("CK_PRDT_SLUG_HIST_SLUG", "\"SLUG\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "FK_PRDT_SLUG_HIST_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "CTLG",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_VRNT",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    CMPR_PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_VRNT", x => x.ID);
                    table.CheckConstraint("CK_PRDT_VRNT_PRIC", "(\"PRIC\" IS NULL OR \"PRIC\" > 0) AND (\"CMPR_PRIC\" IS NULL OR (\"PRIC\" IS NOT NULL AND \"CMPR_PRIC\" > \"PRIC\"))");
                    table.ForeignKey(
                        name: "FK_PRDT_VRNT_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "CTLG",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_PRIC_HIST",
                schema: "CTLG",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_VRNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    OLD_PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    NEW_PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    OLD_CMPR_PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    NEW_CMPR_PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    CHNG_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_PRIC_HIST", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PRDT_PRIC_HIST_PRDT_VRNT_ID",
                        column: x => x.PRDT_VRNT_ID,
                        principalSchema: "CTLG",
                        principalTable: "PRDT_VRNT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "CTLG",
                table: "PRDT_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "DRAFT", "Being prepared; not visible to shoppers", true, "Draft", (short)1 },
                    { (short)2, "PUBLISHED", "On sale", true, "Published", (short)2 },
                    { (short)3, "UNPUBLISHED", "Taken off sale; kept in history", true, "Unpublished", (short)3 },
                    { (short)4, "ARCHIVED", "Retired for good; its address answers gone", true, "Archived", (short)4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CHRT_LINE_CHRT_ID_SIZE_ID",
                schema: "CTLG",
                table: "CHRT_LINE",
                columns: new[] { "CHRT_ID", "SIZE_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CHRT_MAIN_CATG_ID_GNDR_ID",
                schema: "CTLG",
                table: "CHRT_MAIN",
                columns: new[] { "CATG_ID", "GNDR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CHRT_MAIN_GNDR_ID",
                schema: "CTLG",
                table: "CHRT_MAIN",
                column: "GNDR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_CARE_MAP_CARE_ID",
                schema: "CTLG",
                table: "PRDT_CARE_MAP",
                column: "CARE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_CARE_MAP_PRDT_ID_CARE_ID",
                schema: "CTLG",
                table: "PRDT_CARE_MAP",
                columns: new[] { "PRDT_ID", "CARE_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_CMPS_FIBR_ID",
                schema: "CTLG",
                table: "PRDT_CMPS",
                column: "FIBR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_CMPS_PRDT_ID_FIBR_ID",
                schema: "CTLG",
                table: "PRDT_CMPS",
                columns: new[] { "PRDT_ID", "FIBR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_COLN_MAP_COLN_ID",
                schema: "CTLG",
                table: "PRDT_COLN_MAP",
                column: "COLN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_COLN_MAP_PRDT_ID_COLN_ID",
                schema: "CTLG",
                table: "PRDT_COLN_MAP",
                columns: new[] { "PRDT_ID", "COLN_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_IMG_CLR_ID",
                schema: "CTLG",
                table: "PRDT_IMG",
                column: "CLR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_IMG_PRDT_ID_SORT_SEQ",
                schema: "CTLG",
                table: "PRDT_IMG",
                columns: new[] { "PRDT_ID", "SORT_SEQ" });

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_MAIN_AGE_BRKT_ID",
                schema: "CTLG",
                table: "PRDT_MAIN",
                column: "AGE_BRKT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_MAIN_GNDR_ID",
                schema: "CTLG",
                table: "PRDT_MAIN",
                column: "GNDR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_MAIN_GNDR_ID_CATG_ID_SLUG",
                schema: "CTLG",
                table: "PRDT_MAIN",
                columns: new[] { "GNDR_ID", "CATG_ID", "SLUG" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_MAIN_PRDT_STS_ID_CATG_ID",
                schema: "CTLG",
                table: "PRDT_MAIN",
                columns: new[] { "PRDT_STS_ID", "CATG_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_MAIN_STYL_ID",
                schema: "CTLG",
                table: "PRDT_MAIN",
                column: "STYL_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_PRIC_HIST_PRDT_VRNT_ID",
                schema: "CTLG",
                table: "PRDT_PRIC_HIST",
                column: "PRDT_VRNT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_SLUG_HIST_GNDR_ID_CATG_ID_SLUG",
                schema: "CTLG",
                table: "PRDT_SLUG_HIST",
                columns: new[] { "GNDR_ID", "CATG_ID", "SLUG" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_SLUG_HIST_PRDT_ID",
                schema: "CTLG",
                table: "PRDT_SLUG_HIST",
                column: "PRDT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_STS_LKP_CODE",
                schema: "CTLG",
                table: "PRDT_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_VRNT_PRDT_ID_CLR_ID_SIZE_ID",
                schema: "CTLG",
                table: "PRDT_VRNT",
                columns: new[] { "PRDT_ID", "CLR_ID", "SIZE_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_LBL_LBL_VRNT_ID",
                schema: "CTLG",
                table: "VRNT_LBL",
                column: "LBL_VRNT_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_LBL_STYL_ID_CLR_ID_SIZE_ID",
                schema: "CTLG",
                table: "VRNT_LBL",
                columns: new[] { "STYL_ID", "CLR_ID", "SIZE_ID" });

            // PRD-7: price history is append-only, whatever writes to it.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION "CTLG"."FN_PRDT_PRIC_HIST_NO_CHNG"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'CTLG.PRDT_PRIC_HIST is append-only: a price change is a new row';
                END;
                $$;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER "TR_PRDT_PRIC_HIST_NO_CHNG" BEFORE UPDATE OR DELETE ON "CTLG"."PRDT_PRIC_HIST"
                FOR EACH ROW EXECUTE FUNCTION "CTLG"."FN_PRDT_PRIC_HIST_NO_CHNG"();
                """);

            // MED-5: a published product's image must keep real alt text, by any route (defence in depth for SEO-07).
            migrationBuilder.Sql(
                """
                CREATE FUNCTION "CTLG"."FN_PRDT_IMG_ALT_PUBL"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW."ALT" IS NULL OR btrim(NEW."ALT") = '')
                       AND EXISTS (SELECT 1 FROM "CTLG"."PRDT_MAIN" WHERE "ID" = NEW."PRDT_ID" AND "PRDT_STS_ID" = 2) THEN
                        RAISE EXCEPTION 'A published product''s image needs alt text (CTLG.PRDT_IMG.ALT)';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER "TR_PRDT_IMG_ALT_PUBL" BEFORE INSERT OR UPDATE ON "CTLG"."PRDT_IMG"
                FOR EACH ROW EXECUTE FUNCTION "CTLG"."FN_PRDT_IMG_ALT_PUBL"();
                """);

            // MED-5/MED-1: a product cannot move to Published while one of its images has no alt text.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION "CTLG"."FN_PRDT_MAIN_PUBL_ALT"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."PRDT_STS_ID" = 2 AND OLD."PRDT_STS_ID" <> 2
                       AND EXISTS (SELECT 1 FROM "CTLG"."PRDT_IMG" WHERE "PRDT_ID" = NEW."ID" AND ("ALT" IS NULL OR btrim("ALT") = '')) THEN
                        RAISE EXCEPTION 'A product cannot be published while an image has no alt text (CTLG.PRDT_MAIN)';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER "TR_PRDT_MAIN_PUBL_ALT" BEFORE UPDATE ON "CTLG"."PRDT_MAIN"
                FOR EACH ROW EXECUTE FUNCTION "CTLG"."FN_PRDT_MAIN_PUBL_ALT"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER "TR_PRDT_MAIN_PUBL_ALT" ON "CTLG"."PRDT_MAIN";""");
            migrationBuilder.Sql("""DROP FUNCTION "CTLG"."FN_PRDT_MAIN_PUBL_ALT"();""");
            migrationBuilder.Sql("""DROP TRIGGER "TR_PRDT_IMG_ALT_PUBL" ON "CTLG"."PRDT_IMG";""");
            migrationBuilder.Sql("""DROP FUNCTION "CTLG"."FN_PRDT_IMG_ALT_PUBL"();""");
            migrationBuilder.Sql("""DROP TRIGGER "TR_PRDT_PRIC_HIST_NO_CHNG" ON "CTLG"."PRDT_PRIC_HIST";""");
            migrationBuilder.Sql("""DROP FUNCTION "CTLG"."FN_PRDT_PRIC_HIST_NO_CHNG"();""");

            migrationBuilder.DropTable(
                name: "CHRT_LINE",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "INBX",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "PRDT_CARE_MAP",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "PRDT_CMPS",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "PRDT_COLN_MAP",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "PRDT_IMG",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "PRDT_PRIC_HIST",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "PRDT_SLUG_HIST",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "VRNT_LBL",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "CHRT_MAIN",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "PRDT_VRNT",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "PRDT_MAIN",
                schema: "CTLG");

            migrationBuilder.DropTable(
                name: "PRDT_STS_LKP",
                schema: "CTLG");
        }
    }
}
