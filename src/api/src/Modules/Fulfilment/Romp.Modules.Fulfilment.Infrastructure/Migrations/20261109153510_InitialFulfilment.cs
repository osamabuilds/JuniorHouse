using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // generated migration: constant arrays are fine here

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Fulfilment.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialFulfilment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "FULF");

            migrationBuilder.CreateTable(
                name: "EXCP_RSN_LKP",
                schema: "FULF",
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
                    table.PrimaryKey("PK_EXCP_RSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "EXCP_STS_LKP",
                schema: "FULF",
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
                    table.PrimaryKey("PK_EXCP_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FULF_STS_LKP",
                schema: "FULF",
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
                    table.PrimaryKey("PK_FULF_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "FULF",
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
                name: "LINE_STS_LKP",
                schema: "FULF",
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
                    table.PrimaryKey("PK_LINE_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OUTB_MSG",
                schema: "FULF",
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
                name: "PACK_KIND_LKP",
                schema: "FULF",
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
                    table.PrimaryKey("PK_PACK_KIND_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SCAN_OTCM_LKP",
                schema: "FULF",
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
                    table.PrimaryKey("PK_SCAN_OTCM_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SCAN_STG_LKP",
                schema: "FULF",
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
                    table.PrimaryKey("PK_SCAN_STG_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FULF_ORDR",
                schema: "FULF",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    ORDR_NO = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PLCE_KEY = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FULF_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    COD_IND = table.Column<bool>(type: "boolean", nullable: false),
                    CASH_AMT = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    QUED_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PICK_CLM_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PICK_CLM_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CRNT_BIN = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    PICK_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PICK_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PACK_CLM_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PACK_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PACK_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PTBK_IND = table.Column<bool>(type: "boolean", nullable: false),
                    SLIP_PRT_CNT = table.Column<int>(type: "integer", nullable: false),
                    SLIP_PRT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SLIP_PRT_PEND_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FULF_ORDR", x => x.ID);
                    table.CheckConstraint("CK_FULF_ORDR_CASH_AMT", "\"CASH_AMT\" >= 0");
                    table.ForeignKey(
                        name: "FK_FULF_ORDR_FULF_STS_LKP",
                        column: x => x.FULF_STS_ID,
                        principalSchema: "FULF",
                        principalTable: "FULF_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FULF_SCAN",
                schema: "FULF",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    SCAN_STG_ID = table.Column<short>(type: "smallint", nullable: false),
                    SCAN_CODE = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SCAN_OTCM_ID = table.Column<short>(type: "smallint", nullable: false),
                    RSLT_CODE = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SCAN_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FULF_SCAN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FULF_SCAN_SCAN_OTCM_LKP",
                        column: x => x.SCAN_OTCM_ID,
                        principalSchema: "FULF",
                        principalTable: "SCAN_OTCM_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FULF_SCAN_SCAN_STG_LKP",
                        column: x => x.SCAN_STG_ID,
                        principalSchema: "FULF",
                        principalTable: "SCAN_STG_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FULF_EXCP",
                schema: "FULF",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    VRNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    BIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    BIN_CODE = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    EXCP_RSN_ID = table.Column<short>(type: "smallint", nullable: false),
                    EXCP_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    SYS_QTY = table.Column<int>(type: "integer", nullable: false),
                    FND_QTY = table.Column<int>(type: "integer", nullable: false),
                    RAIS_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RAIS_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RSLV_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RSLV_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RSTK_IND = table.Column<bool>(type: "boolean", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FULF_EXCP", x => x.ID);
                    table.CheckConstraint("CK_FULF_EXCP_QTY", "\"SYS_QTY\" >= 0 AND \"FND_QTY\" >= 0");
                    table.ForeignKey(
                        name: "FK_FULF_EXCP_EXCP_RSN_LKP",
                        column: x => x.EXCP_RSN_ID,
                        principalSchema: "FULF",
                        principalTable: "EXCP_RSN_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FULF_EXCP_EXCP_STS_LKP",
                        column: x => x.EXCP_STS_ID,
                        principalSchema: "FULF",
                        principalTable: "EXCP_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FULF_EXCP_FULF_ORDR",
                        column: x => x.ORDR_ID,
                        principalSchema: "FULF",
                        principalTable: "FULF_ORDR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FULF_LINE",
                schema: "FULF",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    VRNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    SKU_CODE = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    GTIN = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    PRDT_NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CLR_NAME = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SIZE_NAME = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    UNIT_PRIC = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    LINE_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    PICK_QTY = table.Column<int>(type: "integer", nullable: false),
                    PACK_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FULF_LINE", x => x.ID);
                    table.CheckConstraint("CK_FULF_LINE_PACK_QTY", "\"PACK_QTY\" >= 0 AND \"PACK_QTY\" <= \"QTY\"");
                    table.CheckConstraint("CK_FULF_LINE_PICK_QTY", "\"PICK_QTY\" >= 0 AND \"PICK_QTY\" <= \"QTY\"");
                    table.CheckConstraint("CK_FULF_LINE_QTY", "\"QTY\" > 0");
                    table.ForeignKey(
                        name: "FK_FULF_LINE_FULF_ORDR",
                        column: x => x.ORDR_ID,
                        principalSchema: "FULF",
                        principalTable: "FULF_ORDR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FULF_LINE_LINE_STS_LKP",
                        column: x => x.LINE_STS_ID,
                        principalSchema: "FULF",
                        principalTable: "LINE_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FULF_PACK",
                schema: "FULF",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    WGHT_GRM = table.Column<int>(type: "integer", nullable: false),
                    EXPC_GRM = table.Column<int>(type: "integer", nullable: false),
                    PACK_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    DVTN_IND = table.Column<bool>(type: "boolean", nullable: false),
                    NOTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WGHT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WGHT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FULF_PACK", x => x.ID);
                    table.CheckConstraint("CK_FULF_PACK_WGHT_GRM", "\"WGHT_GRM\" > 0 AND \"WGHT_GRM\" <= 30000");
                    table.ForeignKey(
                        name: "FK_FULF_PACK_FULF_ORDR",
                        column: x => x.ORDR_ID,
                        principalSchema: "FULF",
                        principalTable: "FULF_ORDR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FULF_PACK_PACK_KIND_LKP",
                        column: x => x.PACK_KIND_ID,
                        principalSchema: "FULF",
                        principalTable: "PACK_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FULF_ALOC",
                schema: "FULF",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LINE_ID = table.Column<long>(type: "bigint", nullable: false),
                    BIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    BIN_CODE = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    WALK_KEY = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    PICK_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FULF_ALOC", x => x.ID);
                    table.CheckConstraint("CK_FULF_ALOC_PICK_QTY", "\"PICK_QTY\" >= 0 AND \"PICK_QTY\" <= \"QTY\"");
                    table.CheckConstraint("CK_FULF_ALOC_QTY", "\"QTY\" >= 0");
                    table.ForeignKey(
                        name: "FK_FULF_ALOC_FULF_LINE",
                        column: x => x.LINE_ID,
                        principalSchema: "FULF",
                        principalTable: "FULF_LINE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "FULF",
                table: "EXCP_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "NOT_FOUND", "The units are not on the shelf", true, "Not found", (short)1 },
                    { (short)2, "SHORT", "Fewer units than the system says", true, "Short", (short)2 },
                    { (short)3, "DAMAGED", "The units on the shelf cannot be sent", true, "Damaged", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "FULF",
                table: "EXCP_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "OPEN", "Waiting for staff", true, "Open", (short)1 },
                    { (short)2, "RESOLVED", "Restocked, or the line was dropped", true, "Resolved", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "FULF",
                table: "FULF_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "QUEUED", "Allocated and waiting to be picked", true, "Queued", (short)1 },
                    { (short)2, "PICKING", "A picker holds the order", true, "Picking", (short)2 },
                    { (short)3, "PICKED", "Every unit scanned and the stock decremented; waiting to be packed", true, "Picked", (short)3 },
                    { (short)4, "PACKING", "A packer holds the order", true, "Packing", (short)4 },
                    { (short)5, "PACKED", "The parcel is sealed and weighed; waiting for the courier", true, "Packed", (short)5 },
                    { (short)6, "HELD", "Every line is waiting for restock", true, "Held", (short)6 },
                    { (short)7, "CANCELLED", "The order was cancelled or every line was dropped", true, "Cancelled", (short)7 }
                });

            migrationBuilder.InsertData(
                schema: "FULF",
                table: "LINE_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "ALLOCATED", "Units are set aside in bins", true, "Allocated", (short)1 },
                    { (short)2, "AWAITING_RESTOCK", "Not enough stock on the shelf; the customer has been told", true, "Awaiting restock", (short)2 },
                    { (short)3, "PICKED", "Every unit scanned at the pick station", true, "Picked", (short)3 },
                    { (short)4, "PACKED", "Every unit scanned at the pack station", true, "Packed", (short)4 },
                    { (short)5, "CANCELLED", "Dropped from the order", true, "Cancelled", (short)5 }
                });

            migrationBuilder.InsertData(
                schema: "FULF",
                table: "PACK_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "POLY_BAG", "A small soft parcel", true, "Poly bag", (short)1 },
                    { (short)2, "BOX", "More than one unit, or a heavier parcel", true, "Box", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "FULF",
                table: "SCAN_OTCM_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "ACCEPTED", "The scan was counted", true, "Accepted", (short)1 },
                    { (short)2, "REFUSED", "The scan was refused and nothing changed", true, "Refused", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "FULF",
                table: "SCAN_STG_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "PICK", "Scanned at the pick station", true, "Pick", (short)1 },
                    { (short)2, "PACK", "Scanned at the pack station", true, "Pack", (short)2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EXCP_RSN_LKP_CODE",
                schema: "FULF",
                table: "EXCP_RSN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EXCP_STS_LKP_CODE",
                schema: "FULF",
                table: "EXCP_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FULF_ALOC_LINE_ID_BIN_CODE",
                schema: "FULF",
                table: "FULF_ALOC",
                columns: new[] { "LINE_ID", "BIN_CODE" });

            migrationBuilder.CreateIndex(
                name: "UQ_FULF_ALOC_LINE_ID_BIN_ID",
                schema: "FULF",
                table: "FULF_ALOC",
                columns: new[] { "LINE_ID", "BIN_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FULF_EXCP_EXCP_RSN_ID",
                schema: "FULF",
                table: "FULF_EXCP",
                column: "EXCP_RSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_FULF_EXCP_EXCP_STS_ID_RAIS_DTE",
                schema: "FULF",
                table: "FULF_EXCP",
                columns: new[] { "EXCP_STS_ID", "RAIS_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_FULF_EXCP_ORDR_ID",
                schema: "FULF",
                table: "FULF_EXCP",
                column: "ORDR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_FULF_LINE_LINE_STS_ID",
                schema: "FULF",
                table: "FULF_LINE",
                column: "LINE_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_FULF_LINE_ORDR_ID_GTIN",
                schema: "FULF",
                table: "FULF_LINE",
                columns: new[] { "ORDR_ID", "GTIN" });

            migrationBuilder.CreateIndex(
                name: "UQ_FULF_LINE_ORDR_ID_VRNT_ID",
                schema: "FULF",
                table: "FULF_LINE",
                columns: new[] { "ORDR_ID", "VRNT_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FULF_ORDR_FULF_STS_ID_QUED_DTE",
                schema: "FULF",
                table: "FULF_ORDR",
                columns: new[] { "FULF_STS_ID", "QUED_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_FULF_ORDR_PLCE_KEY",
                schema: "FULF",
                table: "FULF_ORDR",
                column: "PLCE_KEY");

            migrationBuilder.CreateIndex(
                name: "IX_FULF_ORDR_SLIP_PRT_PEND_IND",
                schema: "FULF",
                table: "FULF_ORDR",
                column: "SLIP_PRT_PEND_IND",
                filter: "\"SLIP_PRT_PEND_IND\"");

            migrationBuilder.CreateIndex(
                name: "UQ_FULF_ORDR_ORDR_ID",
                schema: "FULF",
                table: "FULF_ORDR",
                column: "ORDR_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FULF_PACK_PACK_KIND_ID",
                schema: "FULF",
                table: "FULF_PACK",
                column: "PACK_KIND_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_FULF_PACK_ORDR_ID",
                schema: "FULF",
                table: "FULF_PACK",
                column: "ORDR_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FULF_SCAN_ORDR_ID",
                schema: "FULF",
                table: "FULF_SCAN",
                column: "ORDR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_FULF_SCAN_SCAN_DTE",
                schema: "FULF",
                table: "FULF_SCAN",
                column: "SCAN_DTE");

            migrationBuilder.CreateIndex(
                name: "IX_FULF_SCAN_SCAN_OTCM_ID",
                schema: "FULF",
                table: "FULF_SCAN",
                column: "SCAN_OTCM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_FULF_SCAN_SCAN_STG_ID",
                schema: "FULF",
                table: "FULF_SCAN",
                column: "SCAN_STG_ID");

            migrationBuilder.CreateIndex(
                name: "IX_FULF_STS_LKP_CODE",
                schema: "FULF",
                table: "FULF_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LINE_STS_LKP_CODE",
                schema: "FULF",
                table: "LINE_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PACK_KIND_LKP_CODE",
                schema: "FULF",
                table: "PACK_KIND_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SCAN_OTCM_LKP_CODE",
                schema: "FULF",
                table: "SCAN_OTCM_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SCAN_STG_LKP_CODE",
                schema: "FULF",
                table: "SCAN_STG_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FULF_ALOC",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "FULF_EXCP",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "FULF_PACK",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "FULF_SCAN",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "INBX",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "FULF_LINE",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "EXCP_RSN_LKP",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "EXCP_STS_LKP",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "PACK_KIND_LKP",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "SCAN_OTCM_LKP",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "SCAN_STG_LKP",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "FULF_ORDR",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "LINE_STS_LKP",
                schema: "FULF");

            migrationBuilder.DropTable(
                name: "FULF_STS_LKP",
                schema: "FULF");
        }
    }
}
