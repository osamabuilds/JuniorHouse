using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialStorefront : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "STFR");

            migrationBuilder.CreateTable(
                name: "AGE_STEP",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AGE_CODE = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LBL = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AGE_STEP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "BLCK_TYP_LKP",
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
                    table.PrimaryKey("PK_BLCK_TYP_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DLVR_RULE",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CITY = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    MIN_DAY = table.Column<short>(type: "smallint", nullable: false),
                    MAX_DAY = table.Column<short>(type: "smallint", nullable: false),
                    CHRG_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    FREE_THRS_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    CRER = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DLVR_RULE", x => x.ID);
                    table.CheckConstraint("CK_DLVR_RULE_AMT_POS", "\"CHRG_AMT\" >= 0 AND (\"FREE_THRS_AMT\" IS NULL OR \"FREE_THRS_AMT\" >= 0)");
                    table.CheckConstraint("CK_DLVR_RULE_DAY_ORDR", "\"MIN_DAY\" >= 0 AND \"MAX_DAY\" >= \"MIN_DAY\"");
                });

            migrationBuilder.CreateTable(
                name: "DRFT_MAIN",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VER_NO = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DRFT_MAIN", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FACT_LKP",
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
                    table.PrimaryKey("PK_FACT_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FOOT_COLM",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TTL = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FOOT_COLM", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "LINK_TGT_LKP",
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
                    table.PrimaryKey("PK_LINK_TGT_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NAV_KIND_LKP",
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
                    table.PrimaryKey("PK_NAV_KIND_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PAGE_STS_LKP",
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
                    table.PrimaryKey("PK_PAGE_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "POPL_SRCH",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TEXT = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_POPL_SRCH", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PUBL_SNAP",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VER_NO = table.Column<int>(type: "integer", nullable: false),
                    DOC = table.Column<string>(type: "jsonb", nullable: false),
                    DOC_HASH = table.Column<string>(type: "char(64)", nullable: false),
                    CRNT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    NOTE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PUBL_SNAP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "STNG_KEY_LKP",
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
                    table.PrimaryKey("PK_STNG_KEY_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "XCHG_POLY",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WNDW_DAY = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XCHG_POLY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FOOT_LINK",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LBL = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LINK_TGT_ID = table.Column<short>(type: "smallint", nullable: false),
                    TGT_REF = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FOOT_COLM_ID = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FOOT_LINK", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FOOT_LINK_FOOT_COLM_ID",
                        column: x => x.FOOT_COLM_ID,
                        principalSchema: "STFR",
                        principalTable: "FOOT_COLM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FOOT_LINK_LINK_TGT_ID",
                        column: x => x.LINK_TGT_ID,
                        principalSchema: "STFR",
                        principalTable: "LINK_TGT_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NAV_ITEM",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ITEM_KEY = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LBL = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NAV_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    LINK_TGT_ID = table.Column<short>(type: "smallint", nullable: true),
                    TGT_REF = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    SHOW_AGE_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PRNT_ID = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NAV_ITEM", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NAV_ITEM_LINK_TGT_ID",
                        column: x => x.LINK_TGT_ID,
                        principalSchema: "STFR",
                        principalTable: "LINK_TGT_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NAV_ITEM_NAV_KIND_ID",
                        column: x => x.NAV_KIND_ID,
                        principalSchema: "STFR",
                        principalTable: "NAV_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NAV_ITEM_PRNT_ID",
                        column: x => x.PRNT_ID,
                        principalSchema: "STFR",
                        principalTable: "NAV_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PAGE_MAIN",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SLUG = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TTL = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    META_DSCR = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PAGE_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAGE_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PAGE_MAIN_PAGE_STS_ID",
                        column: x => x.PAGE_STS_ID,
                        principalSchema: "STFR",
                        principalTable: "PAGE_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "STNG_ITEM",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    STNG_KEY_ID = table.Column<short>(type: "smallint", nullable: false),
                    VAL = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STNG_ITEM", x => x.ID);
                    table.ForeignKey(
                        name: "FK_STNG_ITEM_STNG_KEY_ID",
                        column: x => x.STNG_KEY_ID,
                        principalSchema: "STFR",
                        principalTable: "STNG_KEY_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "XCHG_COND",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TEXT = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    XCHG_POLY_ID = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XCHG_COND", x => x.ID);
                    table.ForeignKey(
                        name: "FK_XCHG_COND_XCHG_POLY_ID",
                        column: x => x.XCHG_POLY_ID,
                        principalSchema: "STFR",
                        principalTable: "XCHG_POLY",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PAGE_BLCK",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BLCK_TYP_ID = table.Column<short>(type: "smallint", nullable: false),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    TEXT = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    LVL = table.Column<short>(type: "smallint", nullable: false),
                    FACT_ID = table.Column<short>(type: "smallint", nullable: true),
                    LBL = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ORDR_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PAGE_MAIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAGE_BLCK", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PAGE_BLCK_BLCK_TYP_ID",
                        column: x => x.BLCK_TYP_ID,
                        principalSchema: "STFR",
                        principalTable: "BLCK_TYP_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAGE_BLCK_FACT_ID",
                        column: x => x.FACT_ID,
                        principalSchema: "STFR",
                        principalTable: "FACT_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PAGE_BLCK_PAGE_MAIN_ID",
                        column: x => x.PAGE_MAIN_ID,
                        principalSchema: "STFR",
                        principalTable: "PAGE_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PAGE_RDIR",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OLD_SLUG = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PAGE_MAIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAGE_RDIR", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PAGE_RDIR_PAGE_MAIN_ID",
                        column: x => x.PAGE_MAIN_ID,
                        principalSchema: "STFR",
                        principalTable: "PAGE_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PAGE_BLCK_ITM",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ROW_NO = table.Column<short>(type: "smallint", nullable: false),
                    COLM_NO = table.Column<short>(type: "smallint", nullable: false),
                    TEXT = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PAGE_BLCK_ID = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PAGE_BLCK_ITM", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PAGE_BLCK_ITM_PAGE_BLCK_ID",
                        column: x => x.PAGE_BLCK_ID,
                        principalSchema: "STFR",
                        principalTable: "PAGE_BLCK",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "BLCK_TYP_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "HEADING", "Level 2 or 3", true, "Heading", (short)1 },
                    { (short)2, "PARAGRAPH", "Plain text", true, "Paragraph", (short)2 },
                    { (short)3, "LIST", "Ordered plain-text items", true, "List", (short)3 },
                    { (short)4, "TABLE", "Rows and columns of plain text", true, "Table", (short)4 },
                    { (short)5, "CALLOUT", "A highlighted plain-text note", true, "Callout", (short)5 },
                    { (short)6, "FACT", "A value taken from the settings", true, "Fact", (short)6 }
                });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "FACT_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "contact.whatsapp", "Quoted by fact blocks; empty facts block publishing", true, "contact.whatsapp", (short)1 },
                    { (short)2, "contact.email", "Quoted by fact blocks; empty facts block publishing", true, "contact.email", (short)2 },
                    { (short)3, "contact.hours", "Quoted by fact blocks; empty facts block publishing", true, "contact.hours", (short)3 },
                    { (short)4, "exchange.window", "Quoted by fact blocks; empty facts block publishing", true, "exchange.window", (short)4 },
                    { (short)5, "exchange.conditions", "Quoted by fact blocks; empty facts block publishing", true, "exchange.conditions", (short)5 },
                    { (short)6, "delivery.rules", "Quoted by fact blocks; empty facts block publishing", true, "delivery.rules", (short)6 }
                });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "LINK_TGT_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "PATH", "A path on the shop, for example /girls/dresses", true, "Path", (short)1 },
                    { (short)2, "PAGE", "A published content page, by slug", true, "Page", (short)2 },
                    { (short)3, "COLLECTION", "A catalogue collection, by code", true, "Collection", (short)3 },
                    { (short)4, "CATEGORY", "A catalogue category, by code", true, "Category", (short)4 },
                    { (short)5, "EXTERNAL", "An https address on the allowed-sites list", true, "External", (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "NAV_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "LINK", "Opens its target; may also have members", true, "Link", (short)1 },
                    { (short)2, "GROUP", "Only opens its members", true, "Group", (short)2 },
                    { (short)3, "COLLECTION_LIST", "Lists the enabled collections as members", true, "Collection list", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "PAGE_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "DRAFT", "Being written; not on the shop", true, "Draft", (short)1 },
                    { (short)2, "PUBLISHED", "On the shop", true, "Published", (short)2 },
                    { (short)3, "ARCHIVED", "Removed; its address answers 410", true, "Archived", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "STFR",
                table: "STNG_KEY_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "BRAND_NAME", null, true, "Brand name", (short)1 },
                    { (short)2, "TAGLINE", null, true, "Tagline", (short)2 },
                    { (short)3, "WHATSAPP", "International format, for example +923001234567", true, "WhatsApp number", (short)3 },
                    { (short)4, "EMAIL", null, true, "Email address", (short)4 },
                    { (short)5, "HOURS", null, true, "Opening hours", (short)5 },
                    { (short)6, "ANNOUNCEMENT", "Shown in the bar above the header", true, "Announcement text", (short)6 },
                    { (short)7, "ANNOUNCEMENT_ON", "true or false", true, "Announcement shown", (short)7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AGE_STEP_AGE_CODE",
                schema: "STFR",
                table: "AGE_STEP",
                column: "AGE_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BLCK_TYP_LKP_CODE",
                schema: "STFR",
                table: "BLCK_TYP_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DLVR_RULE_CITY",
                schema: "STFR",
                table: "DLVR_RULE",
                column: "CITY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FACT_LKP_CODE",
                schema: "STFR",
                table: "FACT_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FOOT_LINK_FOOT_COLM_ID",
                schema: "STFR",
                table: "FOOT_LINK",
                column: "FOOT_COLM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_FOOT_LINK_LINK_TGT_ID",
                schema: "STFR",
                table: "FOOT_LINK",
                column: "LINK_TGT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_LINK_TGT_LKP_CODE",
                schema: "STFR",
                table: "LINK_TGT_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NAV_ITEM_ITEM_KEY",
                schema: "STFR",
                table: "NAV_ITEM",
                column: "ITEM_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NAV_ITEM_LINK_TGT_ID",
                schema: "STFR",
                table: "NAV_ITEM",
                column: "LINK_TGT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NAV_ITEM_NAV_KIND_ID",
                schema: "STFR",
                table: "NAV_ITEM",
                column: "NAV_KIND_ID");

            migrationBuilder.CreateIndex(
                name: "IX_NAV_ITEM_PRNT_ID_SORT_SEQ",
                schema: "STFR",
                table: "NAV_ITEM",
                columns: new[] { "PRNT_ID", "SORT_SEQ" });

            migrationBuilder.CreateIndex(
                name: "IX_NAV_KIND_LKP_CODE",
                schema: "STFR",
                table: "NAV_KIND_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAGE_BLCK_BLCK_TYP_ID",
                schema: "STFR",
                table: "PAGE_BLCK",
                column: "BLCK_TYP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAGE_BLCK_FACT_ID",
                schema: "STFR",
                table: "PAGE_BLCK",
                column: "FACT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAGE_BLCK_PAGE_MAIN_ID",
                schema: "STFR",
                table: "PAGE_BLCK",
                column: "PAGE_MAIN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAGE_BLCK_ITM_PAGE_BLCK_ID",
                schema: "STFR",
                table: "PAGE_BLCK_ITM",
                column: "PAGE_BLCK_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAGE_MAIN_PAGE_STS_ID",
                schema: "STFR",
                table: "PAGE_MAIN",
                column: "PAGE_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAGE_MAIN_SLUG",
                schema: "STFR",
                table: "PAGE_MAIN",
                column: "SLUG",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAGE_RDIR_OLD_SLUG",
                schema: "STFR",
                table: "PAGE_RDIR",
                column: "OLD_SLUG",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAGE_RDIR_PAGE_MAIN_ID",
                schema: "STFR",
                table: "PAGE_RDIR",
                column: "PAGE_MAIN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAGE_STS_LKP_CODE",
                schema: "STFR",
                table: "PAGE_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_POPL_SRCH_TEXT",
                schema: "STFR",
                table: "POPL_SRCH",
                column: "TEXT",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PUBL_SNAP_VER_NO",
                schema: "STFR",
                table: "PUBL_SNAP",
                column: "VER_NO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_PUBL_SNAP_CRNT_IND",
                schema: "STFR",
                table: "PUBL_SNAP",
                column: "CRNT_IND",
                unique: true,
                filter: "\"CRNT_IND\"");

            migrationBuilder.CreateIndex(
                name: "IX_STNG_ITEM_STNG_KEY_ID",
                schema: "STFR",
                table: "STNG_ITEM",
                column: "STNG_KEY_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_STNG_KEY_LKP_CODE",
                schema: "STFR",
                table: "STNG_KEY_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XCHG_COND_XCHG_POLY_ID",
                schema: "STFR",
                table: "XCHG_COND",
                column: "XCHG_POLY_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AGE_STEP",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "DLVR_RULE",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "DRFT_MAIN",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "FOOT_LINK",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "NAV_ITEM",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PAGE_BLCK_ITM",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PAGE_RDIR",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "POPL_SRCH",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PUBL_SNAP",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "STNG_ITEM",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "XCHG_COND",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "FOOT_COLM",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "LINK_TGT_LKP",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "NAV_KIND_LKP",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PAGE_BLCK",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "STNG_KEY_LKP",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "XCHG_POLY",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "BLCK_TYP_LKP",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "FACT_LKP",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PAGE_MAIN",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "PAGE_STS_LKP",
                schema: "STFR");
        }
    }
}
