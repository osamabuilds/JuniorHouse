#pragma warning disable CA1861
﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Quality.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "QC");

            migrationBuilder.CreateTable(
                name: "GRN_NO_SEQ",
                schema: "QC",
                columns: table => new
                {
                    YR = table.Column<short>(type: "smallint", nullable: false),
                    SEQ = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GRN_NO_SEQ", x => x.YR);
                });

            migrationBuilder.CreateTable(
                name: "GRN_RCNL_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_GRN_RCNL_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GRN_STS_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_GRN_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INBD_DLVR",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RUN_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_NO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VNDR_DN_NO = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DSPT_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    FNL_SHIP_IND = table.Column<bool>(type: "boolean", nullable: false),
                    CNCL_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INBD_DLVR", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "QC",
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
                schema: "QC",
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
                name: "QC_AQL_LVL_LKP",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false),
                    AQL_VAL = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DSCR = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SORT_SEQ = table.Column<short>(type: "smallint", nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_AQL_LVL_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QC_BTCH_STS_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_QC_BTCH_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QC_CHK_RSLT_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_QC_CHK_RSLT_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QC_CHK_TYP_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_QC_CHK_TYP_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QC_DFCT_SVRT_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_QC_DFCT_SVRT_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QC_DFCT_TYP_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_QC_DFCT_TYP_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QC_INSP_LVL_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_QC_INSP_LVL_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QC_INSP_MODE_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_QC_INSP_MODE_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QC_INSP_RSLT_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_QC_INSP_RSLT_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QC_OTCM_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_QC_OTCM_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "QC_SAFE_TYP_LKP",
                schema: "QC",
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
                    table.PrimaryKey("PK_QC_SAFE_TYP_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "GRN_MAIN",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GRN_NO = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    INBD_DLVR_ID = table.Column<long>(type: "bigint", nullable: false),
                    RUN_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_NO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VNDR_DN_NO = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RCVD_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    GRN_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    GRN_RCNL_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLM_TOT_QTY = table.Column<int>(type: "integer", nullable: false),
                    CNTD_TOT_QTY = table.Column<int>(type: "integer", nullable: false),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NOTE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GRN_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_GRN_MAIN_GRN_RCNL_ID",
                        column: x => x.GRN_RCNL_ID,
                        principalSchema: "QC",
                        principalTable: "GRN_RCNL_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GRN_MAIN_GRN_STS_ID",
                        column: x => x.GRN_STS_ID,
                        principalSchema: "QC",
                        principalTable: "GRN_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GRN_MAIN_INBD_DLVR_ID",
                        column: x => x.INBD_DLVR_ID,
                        principalSchema: "QC",
                        principalTable: "INBD_DLVR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "INBD_DLVR_LINE",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    INBD_DLVR_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLM_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INBD_DLVR_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_INBD_DLVR_LINE_INBD_DLVR_ID",
                        column: x => x.INBD_DLVR_ID,
                        principalSchema: "QC",
                        principalTable: "INBD_DLVR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QC_AQL_LIMT",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false),
                    CODE_LTR = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    QC_AQL_LVL_ID = table.Column<short>(type: "smallint", nullable: false),
                    ACPT_NO = table.Column<int>(type: "integer", nullable: false),
                    RJCT_NO = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_AQL_LIMT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_AQL_LIMT_QC_AQL_LVL_ID",
                        column: x => x.QC_AQL_LVL_ID,
                        principalSchema: "QC",
                        principalTable: "QC_AQL_LVL_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QC_AQL_SMPL",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false),
                    QC_INSP_LVL_ID = table.Column<short>(type: "smallint", nullable: false),
                    LOT_MIN_QTY = table.Column<int>(type: "integer", nullable: false),
                    LOT_MAX_QTY = table.Column<int>(type: "integer", nullable: false),
                    CODE_LTR = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    SMPL_QTY = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_AQL_SMPL", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_AQL_SMPL_QC_INSP_LVL_ID",
                        column: x => x.QC_INSP_LVL_ID,
                        principalSchema: "QC",
                        principalTable: "QC_INSP_LVL_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GRN_LINE",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GRN_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLM_QTY = table.Column<int>(type: "integer", nullable: false),
                    CNTD_QTY = table.Column<int>(type: "integer", nullable: false),
                    DIFF_QTY = table.Column<int>(type: "integer", nullable: false),
                    EXCS_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GRN_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_GRN_LINE_GRN_ID",
                        column: x => x.GRN_ID,
                        principalSchema: "QC",
                        principalTable: "GRN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QC_BTCH_MAIN",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GRN_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_NO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    QC_BTCH_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    LOT_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_BTCH_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_BTCH_MAIN_GRN_ID",
                        column: x => x.GRN_ID,
                        principalSchema: "QC",
                        principalTable: "GRN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QC_BTCH_MAIN_QC_BTCH_STS_ID",
                        column: x => x.QC_BTCH_STS_ID,
                        principalSchema: "QC",
                        principalTable: "QC_BTCH_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QC_BTCH_LINE",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BTCH_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_BTCH_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_BTCH_LINE_BTCH_ID",
                        column: x => x.BTCH_ID,
                        principalSchema: "QC",
                        principalTable: "QC_BTCH_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QC_BTCH_SPLT",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BTCH_ID = table.Column<long>(type: "bigint", nullable: false),
                    SPLT_NO = table.Column<short>(type: "smallint", nullable: false),
                    QC_OTCM_ID = table.Column<short>(type: "smallint", nullable: true),
                    RSN = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DCSN_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RWRK_RND_CNT = table.Column<short>(type: "smallint", nullable: false),
                    QC_BTCH_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_BTCH_SPLT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_BTCH_SPLT_BTCH_ID",
                        column: x => x.BTCH_ID,
                        principalSchema: "QC",
                        principalTable: "QC_BTCH_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QC_BTCH_SPLT_QC_BTCH_STS_ID",
                        column: x => x.QC_BTCH_STS_ID,
                        principalSchema: "QC",
                        principalTable: "QC_BTCH_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QC_BTCH_SPLT_QC_OTCM_ID",
                        column: x => x.QC_OTCM_ID,
                        principalSchema: "QC",
                        principalTable: "QC_OTCM_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QC_BTCH_SPLT_LINE",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SPLT_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    RTRN_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_BTCH_SPLT_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_BTCH_SPLT_LINE_SPLT_ID",
                        column: x => x.SPLT_ID,
                        principalSchema: "QC",
                        principalTable: "QC_BTCH_SPLT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QC_INSP_MAIN",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BTCH_ID = table.Column<long>(type: "bigint", nullable: false),
                    SPLT_ID = table.Column<long>(type: "bigint", nullable: true),
                    RND_NO = table.Column<short>(type: "smallint", nullable: false),
                    QC_INSP_MODE_ID = table.Column<short>(type: "smallint", nullable: false),
                    QC_INSP_LVL_ID = table.Column<short>(type: "smallint", nullable: false),
                    QC_AQL_LVL_ID = table.Column<short>(type: "smallint", nullable: false),
                    LOT_QTY = table.Column<int>(type: "integer", nullable: false),
                    SMPL_QTY = table.Column<int>(type: "integer", nullable: false),
                    ACPT_NO = table.Column<int>(type: "integer", nullable: false),
                    RJCT_NO = table.Column<int>(type: "integer", nullable: false),
                    QC_INSP_RSLT_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSP_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_INSP_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_INSP_MAIN_BTCH_ID",
                        column: x => x.BTCH_ID,
                        principalSchema: "QC",
                        principalTable: "QC_BTCH_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QC_INSP_MAIN_QC_AQL_LVL_ID",
                        column: x => x.QC_AQL_LVL_ID,
                        principalSchema: "QC",
                        principalTable: "QC_AQL_LVL_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QC_INSP_MAIN_QC_INSP_LVL_ID",
                        column: x => x.QC_INSP_LVL_ID,
                        principalSchema: "QC",
                        principalTable: "QC_INSP_LVL_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QC_INSP_MAIN_QC_INSP_MODE_ID",
                        column: x => x.QC_INSP_MODE_ID,
                        principalSchema: "QC",
                        principalTable: "QC_INSP_MODE_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QC_INSP_MAIN_QC_INSP_RSLT_ID",
                        column: x => x.QC_INSP_RSLT_ID,
                        principalSchema: "QC",
                        principalTable: "QC_INSP_RSLT_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QC_INSP_MAIN_SPLT_ID",
                        column: x => x.SPLT_ID,
                        principalSchema: "QC",
                        principalTable: "QC_BTCH_SPLT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QC_RWRK_RTRN",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SPLT_ID = table.Column<long>(type: "bigint", nullable: false),
                    RTRN_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NOTE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_RWRK_RTRN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_RWRK_RTRN_SPLT_ID",
                        column: x => x.SPLT_ID,
                        principalSchema: "QC",
                        principalTable: "QC_BTCH_SPLT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QC_DFCT",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    INSP_ID = table.Column<long>(type: "bigint", nullable: false),
                    QC_DFCT_TYP_ID = table.Column<short>(type: "smallint", nullable: false),
                    QC_DFCT_SVRT_ID = table.Column<short>(type: "smallint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: true),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: true),
                    NOTE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_DFCT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_DFCT_INSP_ID",
                        column: x => x.INSP_ID,
                        principalSchema: "QC",
                        principalTable: "QC_INSP_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QC_DFCT_QC_DFCT_SVRT_ID",
                        column: x => x.QC_DFCT_SVRT_ID,
                        principalSchema: "QC",
                        principalTable: "QC_DFCT_SVRT_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QC_DFCT_QC_DFCT_TYP_ID",
                        column: x => x.QC_DFCT_TYP_ID,
                        principalSchema: "QC",
                        principalTable: "QC_DFCT_TYP_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QC_INSP_CHK",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    INSP_ID = table.Column<long>(type: "bigint", nullable: false),
                    QC_CHK_TYP_ID = table.Column<short>(type: "smallint", nullable: false),
                    QC_CHK_RSLT_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_INSP_CHK", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_INSP_CHK_INSP_ID",
                        column: x => x.INSP_ID,
                        principalSchema: "QC",
                        principalTable: "QC_INSP_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QC_INSP_CHK_QC_CHK_RSLT_ID",
                        column: x => x.QC_CHK_RSLT_ID,
                        principalSchema: "QC",
                        principalTable: "QC_CHK_RSLT_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QC_INSP_CHK_QC_CHK_TYP_ID",
                        column: x => x.QC_CHK_TYP_ID,
                        principalSchema: "QC",
                        principalTable: "QC_CHK_TYP_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QC_INSP_SAFE",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    INSP_ID = table.Column<long>(type: "bigint", nullable: false),
                    QC_SAFE_TYP_ID = table.Column<short>(type: "smallint", nullable: false),
                    QC_CHK_RSLT_ID = table.Column<short>(type: "smallint", nullable: false),
                    RSN = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_INSP_SAFE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_INSP_SAFE_INSP_ID",
                        column: x => x.INSP_ID,
                        principalSchema: "QC",
                        principalTable: "QC_INSP_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QC_INSP_SAFE_QC_CHK_RSLT_ID",
                        column: x => x.QC_CHK_RSLT_ID,
                        principalSchema: "QC",
                        principalTable: "QC_CHK_RSLT_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QC_INSP_SAFE_QC_SAFE_TYP_ID",
                        column: x => x.QC_SAFE_TYP_ID,
                        principalSchema: "QC",
                        principalTable: "QC_SAFE_TYP_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QC_INSP_SAFE_LINE",
                schema: "QC",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    INSP_SAFE_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QC_INSP_SAFE_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QC_INSP_SAFE_LINE_INSP_SAFE_ID",
                        column: x => x.INSP_SAFE_ID,
                        principalSchema: "QC",
                        principalTable: "QC_INSP_SAFE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "GRN_RCNL_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Matched", null, true, "Matched the delivery note", (short)1 },
                    { (short)2, "Short", null, true, "Short of the delivery note", (short)2 },
                    { (short)3, "Over", null, true, "Over the delivery note", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "GRN_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[] { (short)1, "Recorded", null, true, "Recorded", (short)1 });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_AQL_LVL_LKP",
                columns: new[] { "ID", "AQL_VAL", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, 0.65m, "Aql0_65", null, true, "AQL 0.65", (short)1 },
                    { (short)2, 1.0m, "Aql1_0", null, true, "AQL 1.0", (short)2 },
                    { (short)3, 1.5m, "Aql1_5", null, true, "AQL 1.5", (short)3 },
                    { (short)4, 2.5m, "Aql2_5", null, true, "AQL 2.5", (short)4 },
                    { (short)5, 4.0m, "Aql4_0", null, true, "AQL 4.0", (short)5 },
                    { (short)6, 6.5m, "Aql6_5", null, true, "AQL 6.5", (short)6 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_BTCH_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Pending", null, true, "Pending inspection", (short)1 },
                    { (short)2, "InInspection", null, true, "In inspection", (short)2 },
                    { (short)3, "Decided", null, true, "Decided", (short)3 },
                    { (short)4, "AwaitingRework", null, true, "Awaiting rework from the vendor", (short)4 },
                    { (short)5, "Closed", null, true, "Closed", (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_CHK_RSLT_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Pass", null, true, "Pass", (short)1 },
                    { (short)2, "Fail", null, true, "Fail", (short)2 },
                    { (short)3, "NotApplicable", null, true, "Not applicable", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_CHK_TYP_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Stitching", null, true, "Stitching", (short)1 },
                    { (short)2, "Measurements", null, true, "Measurements against the size chart", (short)2 },
                    { (short)3, "FabricColourMatch", null, true, "Fabric and colour match to the approved sample", (short)3 },
                    { (short)4, "TrimsSecure", null, true, "Trims, buttons and zips secure", (short)4 },
                    { (short)5, "VisibleDefects", null, true, "No visible defects", (short)5 },
                    { (short)6, "CareLabel", null, true, "Care label content correct", (short)6 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_DFCT_SVRT_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Critical", null, true, "Critical", (short)1 },
                    { (short)2, "Major", null, true, "Major", (short)2 },
                    { (short)3, "Minor", null, true, "Minor", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_DFCT_TYP_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Stitching", null, true, "Stitching", (short)1 },
                    { (short)2, "Measurement", null, true, "Measurement", (short)2 },
                    { (short)3, "FabricColour", null, true, "Fabric or colour", (short)3 },
                    { (short)4, "TrimsSecurity", null, true, "Trims not secure", (short)4 },
                    { (short)5, "VisibleDefect", null, true, "Visible defect", (short)5 },
                    { (short)6, "CareLabel", null, true, "Care label", (short)6 },
                    { (short)7, "Other", null, true, "Other", (short)7 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_INSP_LVL_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "S1", null, false, "Special level S-1", (short)1 },
                    { (short)2, "S2", null, false, "Special level S-2", (short)2 },
                    { (short)3, "S3", null, false, "Special level S-3", (short)3 },
                    { (short)4, "S4", null, false, "Special level S-4", (short)4 },
                    { (short)5, "I", null, true, "General level I", (short)5 },
                    { (short)6, "II", null, true, "General level II", (short)6 },
                    { (short)7, "III", null, true, "General level III", (short)7 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_INSP_MODE_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Aql", null, true, "AQL sampling", (short)1 },
                    { (short)2, "Full", null, true, "100% inspection", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_INSP_RSLT_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Pass", null, true, "Pass", (short)1 },
                    { (short)2, "Fail", null, true, "Fail", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_OTCM_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Accepted", null, true, "Accepted", (short)1 },
                    { (short)2, "Concession", null, true, "Accepted with concession", (short)2 },
                    { (short)3, "Rejected", null, true, "Rejected and returned to the vendor", (short)3 },
                    { (short)4, "Rework", null, true, "Sent back to the vendor for rework", (short)4 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_SAFE_TYP_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "ChokingHazardTrims", null, true, "Choking hazard on small trims", (short)1 },
                    { (short)2, "DrawstringNeckline", null, true, "Drawstring safety on necklines", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_AQL_LIMT",
                columns: new[] { "ID", "ACPT_NO", "QC_AQL_LVL_ID", "CODE_LTR", "RJCT_NO" },
                values: new object[,]
                {
                    { (short)101, 0, (short)1, "H", 1 },
                    { (short)102, 1, (short)1, "J", 2 },
                    { (short)103, 2, (short)1, "K", 3 },
                    { (short)104, 3, (short)1, "L", 4 },
                    { (short)105, 5, (short)1, "M", 6 },
                    { (short)106, 7, (short)1, "N", 8 },
                    { (short)107, 10, (short)1, "P", 11 },
                    { (short)108, 14, (short)1, "Q", 15 },
                    { (short)109, 21, (short)1, "R", 22 },
                    { (short)201, 0, (short)2, "G", 1 },
                    { (short)202, 1, (short)2, "H", 2 },
                    { (short)203, 2, (short)2, "J", 3 },
                    { (short)204, 3, (short)2, "K", 4 },
                    { (short)205, 5, (short)2, "L", 6 },
                    { (short)206, 7, (short)2, "M", 8 },
                    { (short)207, 10, (short)2, "N", 11 },
                    { (short)208, 14, (short)2, "P", 15 },
                    { (short)209, 21, (short)2, "Q", 22 },
                    { (short)301, 0, (short)3, "F", 1 },
                    { (short)302, 1, (short)3, "G", 2 },
                    { (short)303, 2, (short)3, "H", 3 },
                    { (short)304, 3, (short)3, "J", 4 },
                    { (short)305, 5, (short)3, "K", 6 },
                    { (short)306, 7, (short)3, "L", 8 },
                    { (short)307, 10, (short)3, "M", 11 },
                    { (short)308, 14, (short)3, "N", 15 },
                    { (short)309, 21, (short)3, "P", 22 },
                    { (short)401, 0, (short)4, "E", 1 },
                    { (short)402, 1, (short)4, "F", 2 },
                    { (short)403, 2, (short)4, "G", 3 },
                    { (short)404, 3, (short)4, "H", 4 },
                    { (short)405, 5, (short)4, "J", 6 },
                    { (short)406, 7, (short)4, "K", 8 },
                    { (short)407, 10, (short)4, "L", 11 },
                    { (short)408, 14, (short)4, "M", 15 },
                    { (short)409, 21, (short)4, "N", 22 },
                    { (short)501, 0, (short)5, "D", 1 },
                    { (short)502, 1, (short)5, "E", 2 },
                    { (short)503, 2, (short)5, "F", 3 },
                    { (short)504, 3, (short)5, "G", 4 },
                    { (short)505, 5, (short)5, "H", 6 },
                    { (short)506, 7, (short)5, "J", 8 },
                    { (short)507, 10, (short)5, "K", 11 },
                    { (short)508, 14, (short)5, "L", 15 },
                    { (short)509, 21, (short)5, "M", 22 },
                    { (short)601, 0, (short)6, "C", 1 },
                    { (short)602, 1, (short)6, "D", 2 },
                    { (short)603, 2, (short)6, "E", 3 },
                    { (short)604, 3, (short)6, "F", 4 },
                    { (short)605, 5, (short)6, "G", 6 },
                    { (short)606, 7, (short)6, "H", 8 },
                    { (short)607, 10, (short)6, "J", 11 },
                    { (short)608, 14, (short)6, "K", 15 },
                    { (short)609, 21, (short)6, "L", 22 }
                });

            migrationBuilder.InsertData(
                schema: "QC",
                table: "QC_AQL_SMPL",
                columns: new[] { "ID", "CODE_LTR", "QC_INSP_LVL_ID", "LOT_MAX_QTY", "LOT_MIN_QTY", "SMPL_QTY" },
                values: new object[,]
                {
                    { (short)101, "A", (short)5, 8, 1, 2 },
                    { (short)102, "A", (short)5, 15, 9, 2 },
                    { (short)103, "B", (short)5, 25, 16, 3 },
                    { (short)104, "C", (short)5, 50, 26, 5 },
                    { (short)105, "C", (short)5, 90, 51, 5 },
                    { (short)106, "D", (short)5, 150, 91, 8 },
                    { (short)107, "E", (short)5, 280, 151, 13 },
                    { (short)108, "F", (short)5, 500, 281, 20 },
                    { (short)109, "G", (short)5, 1200, 501, 32 },
                    { (short)110, "H", (short)5, 3200, 1201, 50 },
                    { (short)111, "J", (short)5, 10000, 3201, 80 },
                    { (short)112, "K", (short)5, 35000, 10001, 125 },
                    { (short)113, "L", (short)5, 150000, 35001, 200 },
                    { (short)114, "M", (short)5, 500000, 150001, 315 },
                    { (short)115, "N", (short)5, 2147483647, 500001, 500 },
                    { (short)201, "A", (short)6, 8, 1, 2 },
                    { (short)202, "B", (short)6, 15, 9, 3 },
                    { (short)203, "C", (short)6, 25, 16, 5 },
                    { (short)204, "D", (short)6, 50, 26, 8 },
                    { (short)205, "E", (short)6, 90, 51, 13 },
                    { (short)206, "F", (short)6, 150, 91, 20 },
                    { (short)207, "G", (short)6, 280, 151, 32 },
                    { (short)208, "H", (short)6, 500, 281, 50 },
                    { (short)209, "J", (short)6, 1200, 501, 80 },
                    { (short)210, "K", (short)6, 3200, 1201, 125 },
                    { (short)211, "L", (short)6, 10000, 3201, 200 },
                    { (short)212, "M", (short)6, 35000, 10001, 315 },
                    { (short)213, "N", (short)6, 150000, 35001, 500 },
                    { (short)214, "P", (short)6, 500000, 150001, 800 },
                    { (short)215, "Q", (short)6, 2147483647, 500001, 1250 },
                    { (short)301, "B", (short)7, 8, 1, 3 },
                    { (short)302, "C", (short)7, 15, 9, 5 },
                    { (short)303, "D", (short)7, 25, 16, 8 },
                    { (short)304, "E", (short)7, 50, 26, 13 },
                    { (short)305, "F", (short)7, 90, 51, 20 },
                    { (short)306, "G", (short)7, 150, 91, 32 },
                    { (short)307, "H", (short)7, 280, 151, 50 },
                    { (short)308, "J", (short)7, 500, 281, 80 },
                    { (short)309, "K", (short)7, 1200, 501, 125 },
                    { (short)310, "L", (short)7, 3200, 1201, 200 },
                    { (short)311, "M", (short)7, 10000, 3201, 315 },
                    { (short)312, "N", (short)7, 35000, 10001, 500 },
                    { (short)313, "P", (short)7, 150000, 35001, 800 },
                    { (short)314, "Q", (short)7, 500000, 150001, 1250 },
                    { (short)315, "R", (short)7, 2147483647, 500001, 2000 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GRN_LINE_GRN_ID_SIZE_ID_CLR_ID",
                schema: "QC",
                table: "GRN_LINE",
                columns: new[] { "GRN_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GRN_MAIN_GRN_NO",
                schema: "QC",
                table: "GRN_MAIN",
                column: "GRN_NO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GRN_MAIN_GRN_RCNL_ID",
                schema: "QC",
                table: "GRN_MAIN",
                column: "GRN_RCNL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_GRN_MAIN_GRN_STS_ID",
                schema: "QC",
                table: "GRN_MAIN",
                column: "GRN_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_GRN_MAIN_INBD_DLVR_ID",
                schema: "QC",
                table: "GRN_MAIN",
                column: "INBD_DLVR_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GRN_MAIN_PO_ID",
                schema: "QC",
                table: "GRN_MAIN",
                column: "PO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_GRN_RCNL_LKP_CODE",
                schema: "QC",
                table: "GRN_RCNL_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GRN_STS_LKP_CODE",
                schema: "QC",
                table: "GRN_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INBD_DLVR_PO_ID",
                schema: "QC",
                table: "INBD_DLVR",
                column: "PO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INBD_DLVR_RUN_ID_VNDR_DN_NO",
                schema: "QC",
                table: "INBD_DLVR",
                columns: new[] { "RUN_ID", "VNDR_DN_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_INBD_DLVR_LINE_INBD_DLVR_ID_SIZE_ID_CLR_ID",
                schema: "QC",
                table: "INBD_DLVR_LINE",
                columns: new[] { "INBD_DLVR_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_AQL_LIMT_QC_AQL_LVL_ID_CODE_LTR",
                schema: "QC",
                table: "QC_AQL_LIMT",
                columns: new[] { "QC_AQL_LVL_ID", "CODE_LTR" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_AQL_LVL_LKP_CODE",
                schema: "QC",
                table: "QC_AQL_LVL_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_AQL_SMPL_QC_INSP_LVL_ID_LOT_MIN_QTY",
                schema: "QC",
                table: "QC_AQL_SMPL",
                columns: new[] { "QC_INSP_LVL_ID", "LOT_MIN_QTY" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_BTCH_LINE_BTCH_ID_SIZE_ID_CLR_ID",
                schema: "QC",
                table: "QC_BTCH_LINE",
                columns: new[] { "BTCH_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_BTCH_MAIN_GRN_ID",
                schema: "QC",
                table: "QC_BTCH_MAIN",
                column: "GRN_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_BTCH_MAIN_PO_ID",
                schema: "QC",
                table: "QC_BTCH_MAIN",
                column: "PO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_BTCH_MAIN_QC_BTCH_STS_ID",
                schema: "QC",
                table: "QC_BTCH_MAIN",
                column: "QC_BTCH_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_BTCH_SPLT_BTCH_ID_SPLT_NO",
                schema: "QC",
                table: "QC_BTCH_SPLT",
                columns: new[] { "BTCH_ID", "SPLT_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_BTCH_SPLT_QC_BTCH_STS_ID",
                schema: "QC",
                table: "QC_BTCH_SPLT",
                column: "QC_BTCH_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_BTCH_SPLT_QC_OTCM_ID",
                schema: "QC",
                table: "QC_BTCH_SPLT",
                column: "QC_OTCM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_BTCH_SPLT_LINE_SPLT_ID_SIZE_ID_CLR_ID",
                schema: "QC",
                table: "QC_BTCH_SPLT_LINE",
                columns: new[] { "SPLT_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_BTCH_STS_LKP_CODE",
                schema: "QC",
                table: "QC_BTCH_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_CHK_RSLT_LKP_CODE",
                schema: "QC",
                table: "QC_CHK_RSLT_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_CHK_TYP_LKP_CODE",
                schema: "QC",
                table: "QC_CHK_TYP_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_DFCT_INSP_ID",
                schema: "QC",
                table: "QC_DFCT",
                column: "INSP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_DFCT_QC_DFCT_SVRT_ID",
                schema: "QC",
                table: "QC_DFCT",
                column: "QC_DFCT_SVRT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_DFCT_QC_DFCT_TYP_ID",
                schema: "QC",
                table: "QC_DFCT",
                column: "QC_DFCT_TYP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_DFCT_SVRT_LKP_CODE",
                schema: "QC",
                table: "QC_DFCT_SVRT_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_DFCT_TYP_LKP_CODE",
                schema: "QC",
                table: "QC_DFCT_TYP_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_CHK_INSP_ID_QC_CHK_TYP_ID",
                schema: "QC",
                table: "QC_INSP_CHK",
                columns: new[] { "INSP_ID", "QC_CHK_TYP_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_CHK_QC_CHK_RSLT_ID",
                schema: "QC",
                table: "QC_INSP_CHK",
                column: "QC_CHK_RSLT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_CHK_QC_CHK_TYP_ID",
                schema: "QC",
                table: "QC_INSP_CHK",
                column: "QC_CHK_TYP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_LVL_LKP_CODE",
                schema: "QC",
                table: "QC_INSP_LVL_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_MAIN_BTCH_ID_SPLT_ID_RND_NO",
                schema: "QC",
                table: "QC_INSP_MAIN",
                columns: new[] { "BTCH_ID", "SPLT_ID", "RND_NO" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_MAIN_QC_AQL_LVL_ID",
                schema: "QC",
                table: "QC_INSP_MAIN",
                column: "QC_AQL_LVL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_MAIN_QC_INSP_LVL_ID",
                schema: "QC",
                table: "QC_INSP_MAIN",
                column: "QC_INSP_LVL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_MAIN_QC_INSP_MODE_ID",
                schema: "QC",
                table: "QC_INSP_MAIN",
                column: "QC_INSP_MODE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_MAIN_QC_INSP_RSLT_ID",
                schema: "QC",
                table: "QC_INSP_MAIN",
                column: "QC_INSP_RSLT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_MAIN_SPLT_ID",
                schema: "QC",
                table: "QC_INSP_MAIN",
                column: "SPLT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_MODE_LKP_CODE",
                schema: "QC",
                table: "QC_INSP_MODE_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_RSLT_LKP_CODE",
                schema: "QC",
                table: "QC_INSP_RSLT_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_SAFE_INSP_ID_QC_SAFE_TYP_ID",
                schema: "QC",
                table: "QC_INSP_SAFE",
                columns: new[] { "INSP_ID", "QC_SAFE_TYP_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_SAFE_QC_CHK_RSLT_ID",
                schema: "QC",
                table: "QC_INSP_SAFE",
                column: "QC_CHK_RSLT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_SAFE_QC_SAFE_TYP_ID",
                schema: "QC",
                table: "QC_INSP_SAFE",
                column: "QC_SAFE_TYP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_INSP_SAFE_LINE_INSP_SAFE_ID_SIZE_ID_CLR_ID",
                schema: "QC",
                table: "QC_INSP_SAFE_LINE",
                columns: new[] { "INSP_SAFE_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_OTCM_LKP_CODE",
                schema: "QC",
                table: "QC_OTCM_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QC_RWRK_RTRN_SPLT_ID",
                schema: "QC",
                table: "QC_RWRK_RTRN",
                column: "SPLT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_QC_SAFE_TYP_LKP_CODE",
                schema: "QC",
                table: "QC_SAFE_TYP_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GRN_LINE",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "GRN_NO_SEQ",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "INBD_DLVR_LINE",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "INBX",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_AQL_LIMT",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_AQL_SMPL",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_BTCH_LINE",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_BTCH_SPLT_LINE",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_DFCT",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_INSP_CHK",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_INSP_SAFE_LINE",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_RWRK_RTRN",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_DFCT_SVRT_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_DFCT_TYP_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_CHK_TYP_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_INSP_SAFE",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_INSP_MAIN",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_CHK_RSLT_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_SAFE_TYP_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_AQL_LVL_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_INSP_LVL_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_INSP_MODE_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_INSP_RSLT_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_BTCH_SPLT",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_BTCH_MAIN",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_OTCM_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "GRN_MAIN",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "QC_BTCH_STS_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "GRN_RCNL_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "GRN_STS_LKP",
                schema: "QC");

            migrationBuilder.DropTable(
                name: "INBD_DLVR",
                schema: "QC");
        }
    }
}
