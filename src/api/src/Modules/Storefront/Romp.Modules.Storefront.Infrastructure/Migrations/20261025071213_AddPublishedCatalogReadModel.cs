using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishedCatalogReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AVLB_STK",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false),
                    SKU_CODE = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    STCK_VER = table.Column<long>(type: "bigint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AVLB_STK", x => x.ID);
                    table.CheckConstraint("CK_AVLB_STK_QTY_NONNEG", "\"QTY\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "CHRT_MAIN",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false),
                    CNTT_VER = table.Column<long>(type: "bigint", nullable: false),
                    CATG_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CATG_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GNDR_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    GNDR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                name: "DRV_FMT_LKP",
                schema: "STFR",
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
                    table.PrimaryKey("PK_DRV_FMT_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DRV_STS_LKP",
                schema: "STFR",
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
                    table.PrimaryKey("PK_DRV_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "STFR",
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
                name: "MODL_META",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false),
                    MODL_VER = table.Column<int>(type: "integer", nullable: false),
                    LKP_UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MODL_META", x => x.ID);
                    table.CheckConstraint("CK_MODL_META_ONLY_ROW", "\"ID\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "RBLD_STS_LKP",
                schema: "STFR",
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
                    table.PrimaryKey("PK_RBLD_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CHRT_LINE",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CHRT_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SIZE_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SIZE_SEQ = table.Column<short>(type: "smallint", nullable: false),
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
                    table.ForeignKey(
                        name: "FK_CHRT_LINE_CHRT_ID",
                        column: x => x.CHRT_ID,
                        principalSchema: "STFR",
                        principalTable: "CHRT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RBLD_RUN",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RBLD_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    STRT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    END_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PRDT_CNT = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CHRT_CNT = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    STCK_CNT = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ACTR = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RBLD_RUN", x => x.ID);
                    table.CheckConstraint("CK_RBLD_RUN_CNT_NONNEG", "\"PRDT_CNT\" >= 0 AND \"CHRT_CNT\" >= 0 AND \"STCK_CNT\" >= 0");
                    table.ForeignKey(
                        name: "FK_RBLD_RUN_RBLD_STS_ID",
                        column: x => x.RBLD_STS_ID,
                        principalSchema: "STFR",
                        principalTable: "RBLD_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_MAIN",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false),
                    CNTT_VER = table.Column<long>(type: "bigint", nullable: false),
                    PATH = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    STYL_CODE = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GNDR_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    GNDR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CATG_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CATG_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AGE_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AGE_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PUBL_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CARE_NOTE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MIN_PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    MAX_PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    SALE_IND = table.Column<bool>(type: "boolean", nullable: false),
                    MAX_DISC_PCT = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    AVLB_IND = table.Column<bool>(type: "boolean", nullable: false),
                    VSBL_IND = table.Column<bool>(type: "boolean", nullable: false),
                    IMG_RDY_IND = table.Column<bool>(type: "boolean", nullable: false),
                    RBLD_RUN_ID = table.Column<long>(type: "bigint", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_MAIN", x => x.ID);
                    table.CheckConstraint("CK_PRDT_MAIN_DISC_PCT", "\"MAX_DISC_PCT\" >= 0 AND \"MAX_DISC_PCT\" < 100");
                    table.CheckConstraint("CK_PRDT_MAIN_PRIC_ORDR", "\"MIN_PRIC\" IS NULL OR (\"MIN_PRIC\" > 0 AND \"MAX_PRIC\" >= \"MIN_PRIC\")");
                    table.ForeignKey(
                        name: "FK_PRDT_MAIN_RBLD_RUN_ID",
                        column: x => x.RBLD_RUN_ID,
                        principalSchema: "STFR",
                        principalTable: "RBLD_RUN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_CARE",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    CARE_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CARE_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SYMB_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SORT_SEQ = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_CARE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PRDT_CARE_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "STFR",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_CMPS",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    FIBR_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FIBR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PCT = table.Column<decimal>(type: "numeric(4,1)", nullable: false),
                    SORT_SEQ = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_CMPS", x => x.ID);
                    table.CheckConstraint("CK_PRDT_CMPS_PCT_RNG", "\"PCT\" > 0 AND \"PCT\" <= 100");
                    table.ForeignKey(
                        name: "FK_PRDT_CMPS_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "STFR",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_COLN_MAP",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    COLN_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    COLN_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                        principalSchema: "STFR",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_IMG",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: true),
                    FILE_KEY = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HASH = table.Column<string>(type: "character(32)", fixedLength: true, maxLength: 32, nullable: true),
                    WDTH = table.Column<int>(type: "integer", nullable: false),
                    HGHT = table.Column<int>(type: "integer", nullable: false),
                    ALT = table.Column<string>(type: "character varying(125)", maxLength: 125, nullable: false),
                    SORT_SEQ = table.Column<int>(type: "integer", nullable: false),
                    MAIN_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_IMG", x => x.ID);
                    table.CheckConstraint("CK_PRDT_IMG_SIZE_POS", "\"WDTH\" > 0 AND \"HGHT\" > 0");
                    table.ForeignKey(
                        name: "FK_PRDT_IMG_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "STFR",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PRDT_VRNT",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CLR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    SIZE_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SIZE_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SIZE_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    SKU_CODE = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SKU_NORM = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    CMPR_PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    SELL_IND = table.Column<bool>(type: "boolean", nullable: false),
                    AVLB_QTY = table.Column<int>(type: "integer", nullable: true),
                    STCK_VER = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRDT_VRNT", x => x.ID);
                    table.CheckConstraint("CK_PRDT_VRNT_AVLB_QTY", "\"AVLB_QTY\" IS NULL OR \"AVLB_QTY\" >= 0");
                    table.CheckConstraint("CK_PRDT_VRNT_CMPR_PRIC", "\"CMPR_PRIC\" IS NULL OR (\"PRIC\" IS NOT NULL AND \"CMPR_PRIC\" > \"PRIC\")");
                    table.CheckConstraint("CK_PRDT_VRNT_PRIC_POS", "\"PRIC\" IS NULL OR \"PRIC\" > 0");
                    table.ForeignKey(
                        name: "FK_PRDT_VRNT_PRDT_ID",
                        column: x => x.PRDT_ID,
                        principalSchema: "STFR",
                        principalTable: "PRDT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IMG_DRV",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IMG_ID = table.Column<long>(type: "bigint", nullable: false),
                    WDTH = table.Column<int>(type: "integer", nullable: false),
                    DRV_FMT_ID = table.Column<short>(type: "smallint", nullable: false),
                    DRV_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    FILE_KEY = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ATMP_QTY = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NXT_ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ERR_CODE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IMG_DRV", x => x.ID);
                    table.CheckConstraint("CK_IMG_DRV_ATMP_QTY", "\"ATMP_QTY\" >= 0");
                    table.CheckConstraint("CK_IMG_DRV_WDTH_POS", "\"WDTH\" > 0");
                    table.ForeignKey(
                        name: "FK_IMG_DRV_DRV_FMT_ID",
                        column: x => x.DRV_FMT_ID,
                        principalSchema: "STFR",
                        principalTable: "DRV_FMT_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IMG_DRV_DRV_STS_ID",
                        column: x => x.DRV_STS_ID,
                        principalSchema: "STFR",
                        principalTable: "DRV_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IMG_DRV_IMG_ID",
                        column: x => x.IMG_ID,
                        principalSchema: "STFR",
                        principalTable: "PRDT_IMG",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "DRV_FMT_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "WEBP", "The format browsers prefer", true, "WebP", (short)1 },
                    { (short)2, "JPG", "The fallback format", true, "JPEG", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "DRV_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "PENDING", "Waiting for the image worker", true, "Pending", (short)1 },
                    { (short)2, "DONE", "The file exists", true, "Done", (short)2 },
                    { (short)3, "FAILED", "Gave up after the allowed attempts", true, "Failed", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "MODL_META",
                columns: new[] { "ID", "INSR_BY", "INSR_DTE", "LKP_UPDT_DTE", "MODL_VER", "UPDT_BY", "UPDT_DTE" },
                values: new object[] { (short)1, "migration", new DateTimeOffset(new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 0, null, null });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "RBLD_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "RUNNING", "Reading the Catalog and Warehouse contracts", true, "Running", (short)1 },
                    { (short)2, "COMPLETED", "Every row was written and the old ones swept", true, "Completed", (short)2 },
                    { (short)3, "FAILED", "Stopped; the previous rows keep serving", true, "Failed", (short)3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AVLB_STK_SKU_CODE",
                schema: "STFR",
                table: "AVLB_STK",
                column: "SKU_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CHRT_LINE_CHRT_ID_SIZE_CODE",
                schema: "STFR",
                table: "CHRT_LINE",
                columns: new[] { "CHRT_ID", "SIZE_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CHRT_MAIN_CATG_CODE_GNDR_CODE",
                schema: "STFR",
                table: "CHRT_MAIN",
                columns: new[] { "CATG_CODE", "GNDR_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DRV_FMT_LKP_CODE",
                schema: "STFR",
                table: "DRV_FMT_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DRV_STS_LKP_CODE",
                schema: "STFR",
                table: "DRV_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IMG_DRV_DRV_FMT_ID",
                schema: "STFR",
                table: "IMG_DRV",
                column: "DRV_FMT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_IMG_DRV_DRV_STS_ID_NXT_ATMP_DTE",
                schema: "STFR",
                table: "IMG_DRV",
                columns: new[] { "DRV_STS_ID", "NXT_ATMP_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_IMG_DRV_IMG_ID_WDTH_DRV_FMT_ID",
                schema: "STFR",
                table: "IMG_DRV",
                columns: new[] { "IMG_ID", "WDTH", "DRV_FMT_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_CARE_PRDT_ID_CARE_CODE",
                schema: "STFR",
                table: "PRDT_CARE",
                columns: new[] { "PRDT_ID", "CARE_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_CMPS_PRDT_ID_FIBR_CODE",
                schema: "STFR",
                table: "PRDT_CMPS",
                columns: new[] { "PRDT_ID", "FIBR_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_COLN_MAP_COLN_CODE",
                schema: "STFR",
                table: "PRDT_COLN_MAP",
                column: "COLN_CODE");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_COLN_MAP_PRDT_ID_COLN_CODE",
                schema: "STFR",
                table: "PRDT_COLN_MAP",
                columns: new[] { "PRDT_ID", "COLN_CODE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_IMG_PRDT_ID_SORT_SEQ",
                schema: "STFR",
                table: "PRDT_IMG",
                columns: new[] { "PRDT_ID", "SORT_SEQ" });

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_MAIN_GNDR_CODE_CATG_CODE_PUBL_DTE",
                schema: "STFR",
                table: "PRDT_MAIN",
                columns: new[] { "GNDR_CODE", "CATG_CODE", "PUBL_DTE" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_MAIN_MIN_PRIC",
                schema: "STFR",
                table: "PRDT_MAIN",
                column: "MIN_PRIC");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_MAIN_PATH",
                schema: "STFR",
                table: "PRDT_MAIN",
                column: "PATH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_MAIN_RBLD_RUN_ID",
                schema: "STFR",
                table: "PRDT_MAIN",
                column: "RBLD_RUN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_MAIN_VSBL_IND_PUBL_DTE",
                schema: "STFR",
                table: "PRDT_MAIN",
                columns: new[] { "VSBL_IND", "PUBL_DTE" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_VRNT_PRDT_ID",
                schema: "STFR",
                table: "PRDT_VRNT",
                column: "PRDT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_VRNT_SIZE_CODE_CLR_CODE_PRDT_ID",
                schema: "STFR",
                table: "PRDT_VRNT",
                columns: new[] { "SIZE_CODE", "CLR_CODE", "PRDT_ID" },
                filter: "\"AVLB_QTY\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_PRDT_VRNT_SKU_NORM",
                schema: "STFR",
                table: "PRDT_VRNT",
                column: "SKU_NORM",
                unique: true,
                filter: "\"SKU_NORM\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RBLD_RUN_RBLD_STS_ID_STRT_DTE",
                schema: "STFR",
                table: "RBLD_RUN",
                columns: new[] { "RBLD_STS_ID", "STRT_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_RBLD_STS_LKP_CODE",
                schema: "STFR",
                table: "RBLD_STS_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AVLB_STK",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "CHRT_LINE",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "IMG_DRV",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "INBX",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "MODL_META",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PRDT_CARE",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PRDT_CMPS",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PRDT_COLN_MAP",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PRDT_VRNT",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "CHRT_MAIN",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "DRV_FMT_LKP",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "DRV_STS_LKP",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PRDT_IMG",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PRDT_MAIN",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "RBLD_RUN",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "RBLD_STS_LKP",
                schema: "STFR");
        }
    }
}
