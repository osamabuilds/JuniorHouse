#pragma warning disable CA1861
﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Labelling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLabelling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "SKU");

            migrationBuilder.CreateTable(
                name: "GTIN_NO_SEQ",
                schema: "SKU",
                columns: table => new
                {
                    PFX = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    SEQ = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GTIN_NO_SEQ", x => x.PFX);
                });

            migrationBuilder.CreateTable(
                name: "GTIN_STS_LKP",
                schema: "SKU",
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
                    table.PrimaryKey("PK_GTIN_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "SKU",
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
                name: "JOB_NO_SEQ",
                schema: "SKU",
                columns: table => new
                {
                    YR = table.Column<short>(type: "smallint", nullable: false),
                    SEQ = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JOB_NO_SEQ", x => x.YR);
                });

            migrationBuilder.CreateTable(
                name: "LBL_OUTC_LKP",
                schema: "SKU",
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
                    table.PrimaryKey("PK_LBL_OUTC_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "LBL_WORK_STS_LKP",
                schema: "SKU",
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
                    table.PrimaryKey("PK_LBL_WORK_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OUTB_MSG",
                schema: "SKU",
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
                name: "PARK_RSN_LKP",
                schema: "SKU",
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
                    table.PrimaryKey("PK_PARK_RSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PRT_ATMP_OTCM_LKP",
                schema: "SKU",
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
                    table.PrimaryKey("PK_PRT_ATMP_OTCM_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PRT_FMT_LKP",
                schema: "SKU",
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
                    table.PrimaryKey("PK_PRT_FMT_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PRT_JOB_KIND_LKP",
                schema: "SKU",
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
                    table.PrimaryKey("PK_PRT_JOB_KIND_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PRT_JOB_STS_LKP",
                schema: "SKU",
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
                    table.PrimaryKey("PK_PRT_JOB_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RPRT_RSN_LKP",
                schema: "SKU",
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
                    table.PrimaryKey("PK_RPRT_RSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "VOID_RSN_LKP",
                schema: "SKU",
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
                    table.PrimaryKey("PK_VOID_RSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "VRNT_MAIN",
                schema: "SKU",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    STYL_ID = table.Column<long>(type: "bigint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    SKU_CODE = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VRNT_MAIN", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PRTR_LKP",
                schema: "SKU",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false),
                    PRT_FMT_ID = table.Column<short>(type: "smallint", nullable: false),
                    ADDR = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRTR_LKP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PRTR_LKP_PRT_FMT_LKP",
                        column: x => x.PRT_FMT_ID,
                        principalSchema: "SKU",
                        principalTable: "PRT_FMT_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LBL_WORK_ITEM",
                schema: "SKU",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SUB_BTCH_ID = table.Column<long>(type: "bigint", nullable: false),
                    QC_BTCH_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    STYL_ID = table.Column<long>(type: "bigint", nullable: true),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    LBL_OUTC_ID = table.Column<short>(type: "smallint", nullable: false),
                    LBL_WORK_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    PARK_RSN_ID = table.Column<short>(type: "smallint", nullable: true),
                    VRNT_ID = table.Column<long>(type: "bigint", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LBL_WORK_ITEM", x => x.ID);
                    table.ForeignKey(
                        name: "FK_LBL_WORK_ITEM_LBL_OUTC_LKP",
                        column: x => x.LBL_OUTC_ID,
                        principalSchema: "SKU",
                        principalTable: "LBL_OUTC_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LBL_WORK_ITEM_LBL_WORK_STS_LKP",
                        column: x => x.LBL_WORK_STS_ID,
                        principalSchema: "SKU",
                        principalTable: "LBL_WORK_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LBL_WORK_ITEM_PARK_RSN_LKP",
                        column: x => x.PARK_RSN_ID,
                        principalSchema: "SKU",
                        principalTable: "PARK_RSN_LKP",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_LBL_WORK_ITEM_VRNT_MAIN",
                        column: x => x.VRNT_ID,
                        principalSchema: "SKU",
                        principalTable: "VRNT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VRNT_GTIN",
                schema: "SKU",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VRNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    GTIN = table.Column<string>(type: "character(13)", fixedLength: true, maxLength: 13, nullable: false),
                    PFX = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    ITEM_REF = table.Column<long>(type: "bigint", nullable: false),
                    GTIN_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    RETD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RETD_RSN_ID = table.Column<short>(type: "smallint", nullable: true),
                    RETD_NOTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RETD_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VRNT_GTIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_VRNT_GTIN_GTIN_STS_LKP",
                        column: x => x.GTIN_STS_ID,
                        principalSchema: "SKU",
                        principalTable: "GTIN_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VRNT_GTIN_VOID_RSN_LKP",
                        column: x => x.RETD_RSN_ID,
                        principalSchema: "SKU",
                        principalTable: "VOID_RSN_LKP",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_VRNT_GTIN_VRNT_MAIN",
                        column: x => x.VRNT_ID,
                        principalSchema: "SKU",
                        principalTable: "VRNT_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRT_JOB",
                schema: "SKU",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JOB_NO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PRT_JOB_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    VRNT_GTIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    WORK_ITEM_ID = table.Column<long>(type: "bigint", nullable: true),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    LBL_OUTC_ID = table.Column<short>(type: "smallint", nullable: false),
                    PRIC_SNAP = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    PRTR_ID = table.Column<short>(type: "smallint", nullable: false),
                    PRT_JOB_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    ATMP_CNT = table.Column<int>(type: "integer", nullable: false),
                    NXT_ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LAST_ERR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RPRT_RSN_ID = table.Column<short>(type: "smallint", nullable: true),
                    NOTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    REQ_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MNL_ACT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRT_JOB", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PRT_JOB_LBL_OUTC_LKP",
                        column: x => x.LBL_OUTC_ID,
                        principalSchema: "SKU",
                        principalTable: "LBL_OUTC_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PRT_JOB_LBL_WORK_ITEM",
                        column: x => x.WORK_ITEM_ID,
                        principalSchema: "SKU",
                        principalTable: "LBL_WORK_ITEM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PRT_JOB_PRTR_LKP",
                        column: x => x.PRTR_ID,
                        principalSchema: "SKU",
                        principalTable: "PRTR_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PRT_JOB_PRT_JOB_KIND_LKP",
                        column: x => x.PRT_JOB_KIND_ID,
                        principalSchema: "SKU",
                        principalTable: "PRT_JOB_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PRT_JOB_PRT_JOB_STS_LKP",
                        column: x => x.PRT_JOB_STS_ID,
                        principalSchema: "SKU",
                        principalTable: "PRT_JOB_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PRT_JOB_RPRT_RSN_LKP",
                        column: x => x.RPRT_RSN_ID,
                        principalSchema: "SKU",
                        principalTable: "RPRT_RSN_LKP",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_PRT_JOB_VRNT_GTIN",
                        column: x => x.VRNT_GTIN_ID,
                        principalSchema: "SKU",
                        principalTable: "VRNT_GTIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PRT_JOB_ATMP",
                schema: "SKU",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRT_JOB_ID = table.Column<long>(type: "bigint", nullable: false),
                    ATMP_NO = table.Column<int>(type: "integer", nullable: false),
                    PRT_ATMP_OTCM_ID = table.Column<short>(type: "smallint", nullable: false),
                    ERR_TEXT = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OUTP_REF = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PRT_JOB_ATMP", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PRT_JOB_ATMP_PRT_ATMP_OTCM_LKP",
                        column: x => x.PRT_ATMP_OTCM_ID,
                        principalSchema: "SKU",
                        principalTable: "PRT_ATMP_OTCM_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PRT_JOB_ATMP_PRT_JOB",
                        column: x => x.PRT_JOB_ID,
                        principalSchema: "SKU",
                        principalTable: "PRT_JOB",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "GTIN_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Active", null, true, "In use on tags", (short)1 },
                    { (short)2, "Retired", null, true, "Voided and never issued again", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "LBL_OUTC_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Accepted", null, true, "Accepted by QC", (short)1 },
                    { (short)2, "Concession", null, true, "Accepted with concession (2ND mark)", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "LBL_WORK_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Ready", null, true, "Ready to print", (short)1 },
                    { (short)2, "NeedsAttention", null, true, "Needs attention before it can be printed", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "PARK_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "NoStyle", null, true, "The PO's style could not be found", (short)1 },
                    { (short)2, "StyleRetired", null, true, "The style is retired; confirm to label the goods", (short)2 },
                    { (short)3, "UnknownCode", null, true, "A colour or size code could not be found", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "PRT_ATMP_OTCM_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Printed", null, true, "The printer accepted the job", (short)1 },
                    { (short)2, "Failed", null, true, "The attempt failed", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "PRT_FMT_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Zpl", null, true, "ZPL label printer", (short)1 },
                    { (short)2, "Pdf", null, true, "PDF (development preview printer)", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "PRT_JOB_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Original", null, true, "First printing for accepted stock", (short)1 },
                    { (short)2, "Reprint", null, true, "Reprint of an existing tag", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "PRT_JOB_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Queued", null, true, "Waiting for the printer", (short)1 },
                    { (short)2, "Printing", null, true, "Being printed", (short)2 },
                    { (short)3, "Printed", null, true, "Printed", (short)3 },
                    { (short)4, "Failed", null, true, "Failed after all attempts", (short)4 },
                    { (short)5, "Cancelled", null, true, "Cancelled", (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "RPRT_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Lost", null, true, "Tag lost", (short)1 },
                    { (short)2, "Damaged", null, true, "Tag damaged", (short)2 },
                    { (short)3, "PriceChange", null, true, "Price changed", (short)3 },
                    { (short)4, "Other", null, true, "Other reason", (short)4 }
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "VOID_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Error", null, true, "Assigned in error", (short)1 },
                    { (short)2, "Recalled", null, true, "Product recalled", (short)2 },
                    { (short)3, "Other", null, true, "Other reason", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "SKU",
                table: "PRTR_LKP",
                columns: new[] { "ID", "ADDR", "CODE", "DSCR", "PRT_FMT_ID", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "memory", "FAKE-PDF", "Renders the tag as a PDF and discards it.", (short)2, true, "Preview printer (no hardware)", (short)1 },
                    { (short)2, "jam", "FAKE-JAM", "For trying the retry path.", (short)2, true, "Jammed printer (always fails)", (short)2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GTIN_STS_LKP_CODE",
                schema: "SKU",
                table: "GTIN_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LBL_OUTC_LKP_CODE",
                schema: "SKU",
                table: "LBL_OUTC_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LBL_WORK_ITEM_LBL_OUTC_ID",
                schema: "SKU",
                table: "LBL_WORK_ITEM",
                column: "LBL_OUTC_ID");

            migrationBuilder.CreateIndex(
                name: "IX_LBL_WORK_ITEM_PARK_RSN_ID",
                schema: "SKU",
                table: "LBL_WORK_ITEM",
                column: "PARK_RSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_LBL_WORK_ITEM_PO_ID",
                schema: "SKU",
                table: "LBL_WORK_ITEM",
                column: "PO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_LBL_WORK_ITEM_STS_ID",
                schema: "SKU",
                table: "LBL_WORK_ITEM",
                columns: new[] { "LBL_WORK_STS_ID", "ID" });

            migrationBuilder.CreateIndex(
                name: "IX_LBL_WORK_ITEM_SUB_BTCH_ID_SIZE_ID_CLR_ID",
                schema: "SKU",
                table: "LBL_WORK_ITEM",
                columns: new[] { "SUB_BTCH_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LBL_WORK_ITEM_VRNT_ID",
                schema: "SKU",
                table: "LBL_WORK_ITEM",
                column: "VRNT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_LBL_WORK_STS_LKP_CODE",
                schema: "SKU",
                table: "LBL_WORK_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PARK_RSN_LKP_CODE",
                schema: "SKU",
                table: "PARK_RSN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRT_ATMP_OTCM_LKP_CODE",
                schema: "SKU",
                table: "PRT_ATMP_OTCM_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRT_FMT_LKP_CODE",
                schema: "SKU",
                table: "PRT_FMT_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_CLM",
                schema: "SKU",
                table: "PRT_JOB",
                columns: new[] { "PRTR_ID", "PRT_JOB_STS_ID", "NXT_ATMP_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_JOB_NO",
                schema: "SKU",
                table: "PRT_JOB",
                column: "JOB_NO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_LBL_OUTC_ID",
                schema: "SKU",
                table: "PRT_JOB",
                column: "LBL_OUTC_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_PRT_JOB_KIND_ID",
                schema: "SKU",
                table: "PRT_JOB",
                column: "PRT_JOB_KIND_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_PRT_JOB_STS_ID",
                schema: "SKU",
                table: "PRT_JOB",
                column: "PRT_JOB_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_RPRT_RSN_ID",
                schema: "SKU",
                table: "PRT_JOB",
                column: "RPRT_RSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_VRNT_GTIN_ID",
                schema: "SKU",
                table: "PRT_JOB",
                column: "VRNT_GTIN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_WORK_ITEM_ID",
                schema: "SKU",
                table: "PRT_JOB",
                column: "WORK_ITEM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_ATMP_PRT_ATMP_OTCM_ID",
                schema: "SKU",
                table: "PRT_JOB_ATMP",
                column: "PRT_ATMP_OTCM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_ATMP_PRT_JOB_ID",
                schema: "SKU",
                table: "PRT_JOB_ATMP",
                column: "PRT_JOB_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_KIND_LKP_CODE",
                schema: "SKU",
                table: "PRT_JOB_KIND_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRT_JOB_STS_LKP_CODE",
                schema: "SKU",
                table: "PRT_JOB_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRTR_LKP_CODE",
                schema: "SKU",
                table: "PRTR_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PRTR_LKP_PRT_FMT_ID",
                schema: "SKU",
                table: "PRTR_LKP",
                column: "PRT_FMT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_RPRT_RSN_LKP_CODE",
                schema: "SKU",
                table: "RPRT_RSN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VOID_RSN_LKP_CODE",
                schema: "SKU",
                table: "VOID_RSN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_GTIN_GTIN",
                schema: "SKU",
                table: "VRNT_GTIN",
                column: "GTIN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_GTIN_GTIN_STS_ID",
                schema: "SKU",
                table: "VRNT_GTIN",
                column: "GTIN_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_GTIN_PFX_ITEM_REF",
                schema: "SKU",
                table: "VRNT_GTIN",
                columns: new[] { "PFX", "ITEM_REF" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_GTIN_RETD_RSN_ID",
                schema: "SKU",
                table: "VRNT_GTIN",
                column: "RETD_RSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_GTIN_VRNT_STS",
                schema: "SKU",
                table: "VRNT_GTIN",
                columns: new[] { "VRNT_ID", "GTIN_STS_ID" });

            migrationBuilder.CreateIndex(
                name: "UX_VRNT_GTIN_ACT",
                schema: "SKU",
                table: "VRNT_GTIN",
                column: "VRNT_ID",
                unique: true,
                filter: "\"GTIN_STS_ID\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_MAIN_SKU_CODE",
                schema: "SKU",
                table: "VRNT_MAIN",
                column: "SKU_CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_MAIN_STYL_ID_CLR_ID_SIZE_ID",
                schema: "SKU",
                table: "VRNT_MAIN",
                columns: new[] { "STYL_ID", "CLR_ID", "SIZE_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GTIN_NO_SEQ",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "INBX",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "JOB_NO_SEQ",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "PRT_JOB_ATMP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "PRT_ATMP_OTCM_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "PRT_JOB",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "LBL_WORK_ITEM",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "PRTR_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "PRT_JOB_KIND_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "PRT_JOB_STS_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "RPRT_RSN_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "VRNT_GTIN",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "LBL_OUTC_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "LBL_WORK_STS_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "PARK_RSN_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "PRT_FMT_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "GTIN_STS_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "VOID_RSN_LKP",
                schema: "SKU");

            migrationBuilder.DropTable(
                name: "VRNT_MAIN",
                schema: "SKU");
        }
    }
}
