using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1861 // Prefer static readonly fields over constant array arguments (generated code)

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Production.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "PROD");

            migrationBuilder.CreateTable(
                name: "DLVR_QTY_STS_LKP",
                schema: "PROD",
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
                    table.PrimaryKey("PK_DLVR_QTY_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DLVR_TMNG_LKP",
                schema: "PROD",
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
                    table.PrimaryKey("PK_DLVR_TMNG_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "PROD",
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
                name: "MLST_TYP_LKP",
                schema: "PROD",
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
                    table.PrimaryKey("PK_MLST_TYP_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OUTB_MSG",
                schema: "PROD",
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
                name: "PP_SMPL_DCSN_LKP",
                schema: "PROD",
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
                    table.PrimaryKey("PK_PP_SMPL_DCSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PROD_RUN_STS_LKP",
                schema: "PROD",
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
                    table.PrimaryKey("PK_PROD_RUN_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "PROD_RUN_MAIN",
                schema: "PROD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PO_ID = table.Column<long>(type: "bigint", nullable: false),
                    PO_NO = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    FBRC_RESP_ID = table.Column<short>(type: "smallint", nullable: true),
                    PO_SENT_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    LATE_ACPT_DT = table.Column<DateOnly>(type: "date", nullable: true),
                    PROD_RUN_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    EXPC_CMPL_DT = table.Column<DateOnly>(type: "date", nullable: true),
                    CUT_STRT_DT = table.Column<DateOnly>(type: "date", nullable: true),
                    FBRC_DSPT_DT = table.Column<DateOnly>(type: "date", nullable: true),
                    CUT_BEF_FBRC_IND = table.Column<bool>(type: "boolean", nullable: false),
                    RSMP_RECM_IND = table.Column<bool>(type: "boolean", nullable: false),
                    CUT_WOUT_RSMP_IND = table.Column<bool>(type: "boolean", nullable: false),
                    APRV_SUPR_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROD_RUN_MAIN", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PROD_RUN_MAIN_PROD_RUN_STS_ID",
                        column: x => x.PROD_RUN_STS_ID,
                        principalSchema: "PROD",
                        principalTable: "PROD_RUN_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DLVR_NOTE",
                schema: "PROD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RUN_ID = table.Column<long>(type: "bigint", nullable: false),
                    VNDR_DN_NO = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DSPT_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    FNL_SHIP_IND = table.Column<bool>(type: "boolean", nullable: false),
                    REV_NO = table.Column<short>(type: "smallint", nullable: false),
                    DLVR_TMNG_ID = table.Column<short>(type: "smallint", nullable: false),
                    LATE_DAY_CNT = table.Column<int>(type: "integer", nullable: false),
                    DLVR_QTY_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    OVER_QTY = table.Column<int>(type: "integer", nullable: false),
                    SHRT_QTY = table.Column<int>(type: "integer", nullable: false),
                    FBRC_DSPT_DT = table.Column<DateOnly>(type: "date", nullable: true),
                    ROMP_FBRC_DLAY_IND = table.Column<bool>(type: "boolean", nullable: false),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DLVR_NOTE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DLVR_NOTE_DLVR_QTY_STS_ID",
                        column: x => x.DLVR_QTY_STS_ID,
                        principalSchema: "PROD",
                        principalTable: "DLVR_QTY_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DLVR_NOTE_DLVR_TMNG_ID",
                        column: x => x.DLVR_TMNG_ID,
                        principalSchema: "PROD",
                        principalTable: "DLVR_TMNG_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DLVR_NOTE_RUN_ID",
                        column: x => x.RUN_ID,
                        principalSchema: "PROD",
                        principalTable: "PROD_RUN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PP_SMPL_RND",
                schema: "PROD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RUN_ID = table.Column<long>(type: "bigint", nullable: false),
                    RND_NO = table.Column<int>(type: "integer", nullable: false),
                    SUBM_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    SUBM_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NOTE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PP_SMPL_DCSN_ID = table.Column<short>(type: "smallint", nullable: false),
                    DCSN_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DCSN_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DCSN_RSN = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PP_SMPL_RND", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PP_SMPL_RND_PP_SMPL_DCSN_ID",
                        column: x => x.PP_SMPL_DCSN_ID,
                        principalSchema: "PROD",
                        principalTable: "PP_SMPL_DCSN_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PP_SMPL_RND_RUN_ID",
                        column: x => x.RUN_ID,
                        principalSchema: "PROD",
                        principalTable: "PROD_RUN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PROD_EXPC_HIST",
                schema: "PROD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RUN_ID = table.Column<long>(type: "bigint", nullable: false),
                    OLD_DT = table.Column<DateOnly>(type: "date", nullable: true),
                    NEW_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    RSN = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROD_EXPC_HIST", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PROD_EXPC_HIST_RUN_ID",
                        column: x => x.RUN_ID,
                        principalSchema: "PROD",
                        principalTable: "PROD_RUN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DLVR_NOTE_LINE",
                schema: "PROD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NOTE_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    OVER_QTY = table.Column<int>(type: "integer", nullable: false),
                    SHRT_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DLVR_NOTE_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DLVR_NOTE_LINE_NOTE_ID",
                        column: x => x.NOTE_ID,
                        principalSchema: "PROD",
                        principalTable: "DLVR_NOTE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PP_SMPL_FILE",
                schema: "PROD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RND_ID = table.Column<long>(type: "bigint", nullable: false),
                    STOR_KEY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FILE_NAME = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CNTT_TYP = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FILE_SIZE_BYT = table.Column<long>(type: "bigint", nullable: false),
                    DELD_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PP_SMPL_FILE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PP_SMPL_FILE_RND_ID",
                        column: x => x.RND_ID,
                        principalSchema: "PROD",
                        principalTable: "PP_SMPL_RND",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PROD_MLST",
                schema: "PROD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RUN_ID = table.Column<long>(type: "bigint", nullable: false),
                    MLST_TYP_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLM_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    RPTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NOTE = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LATE_RCRD_IND = table.Column<bool>(type: "boolean", nullable: false),
                    PP_RND_ID = table.Column<long>(type: "bigint", nullable: true),
                    FLAG_RSN = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROD_MLST", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PROD_MLST_MLST_TYP_ID",
                        column: x => x.MLST_TYP_ID,
                        principalSchema: "PROD",
                        principalTable: "MLST_TYP_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PROD_MLST_PP_RND_ID",
                        column: x => x.PP_RND_ID,
                        principalSchema: "PROD",
                        principalTable: "PP_SMPL_RND",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PROD_MLST_RUN_ID",
                        column: x => x.RUN_ID,
                        principalSchema: "PROD",
                        principalTable: "PROD_RUN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PROD_MLST_FNSH_LINE",
                schema: "PROD",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MLST_ID = table.Column<long>(type: "bigint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    FNSH_QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PROD_MLST_FNSH_LINE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PROD_MLST_FNSH_LINE_MLST_ID",
                        column: x => x.MLST_ID,
                        principalSchema: "PROD",
                        principalTable: "PROD_MLST",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "PROD",
                table: "DLVR_QTY_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "OnQuantity", null, true, "On quantity", (short)1 },
                    { (short)2, "Short", null, true, "Short", (short)2 },
                    { (short)3, "Over", null, true, "Over", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "PROD",
                table: "DLVR_TMNG_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "OnTime", null, true, "On time", (short)1 },
                    { (short)2, "Late", null, true, "Late", (short)2 },
                    { (short)3, "BeyondAcceptable", null, true, "Beyond the latest acceptable date", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "PROD",
                table: "MLST_TYP_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "FabricBooked", null, true, "Fabric booked", (short)1 },
                    { (short)2, "FabricDispatchedByRomp", null, true, "Fabric dispatched by Romp", (short)2 },
                    { (short)3, "PPSampleSubmitted", null, true, "Pre-production sample submitted", (short)3 },
                    { (short)4, "PPSampleApproved", null, true, "Pre-production sample approved", (short)4 },
                    { (short)5, "PPSampleRejected", null, true, "Pre-production sample rejected", (short)5 },
                    { (short)6, "BulkCuttingStarted", null, true, "Bulk cutting started", (short)6 },
                    { (short)7, "FinishedReadyToShip", null, true, "Finished / ready to ship", (short)7 }
                });

            migrationBuilder.InsertData(
                schema: "PROD",
                table: "PP_SMPL_DCSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Pending", null, true, "Pending", (short)1 },
                    { (short)2, "Approved", null, true, "Approved", (short)2 },
                    { (short)3, "Rejected", null, true, "Rejected", (short)3 }
                });

            migrationBuilder.InsertData(
                schema: "PROD",
                table: "PROD_RUN_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "Open", null, true, "Open", (short)1 },
                    { (short)2, "Cancelled", null, true, "Cancelled", (short)2 },
                    { (short)3, "Closed", null, true, "Closed", (short)3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DLVR_NOTE_DLVR_QTY_STS_ID",
                schema: "PROD",
                table: "DLVR_NOTE",
                column: "DLVR_QTY_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_DLVR_NOTE_DLVR_TMNG_ID",
                schema: "PROD",
                table: "DLVR_NOTE",
                column: "DLVR_TMNG_ID");

            migrationBuilder.CreateIndex(
                name: "IX_DLVR_NOTE_RUN_ID_VNDR_DN_NO",
                schema: "PROD",
                table: "DLVR_NOTE",
                columns: new[] { "RUN_ID", "VNDR_DN_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DLVR_NOTE_LINE_NOTE_ID_SIZE_ID_CLR_ID",
                schema: "PROD",
                table: "DLVR_NOTE_LINE",
                columns: new[] { "NOTE_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DLVR_QTY_STS_LKP_CODE",
                schema: "PROD",
                table: "DLVR_QTY_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DLVR_TMNG_LKP_CODE",
                schema: "PROD",
                table: "DLVR_TMNG_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MLST_TYP_LKP_CODE",
                schema: "PROD",
                table: "MLST_TYP_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PP_SMPL_DCSN_LKP_CODE",
                schema: "PROD",
                table: "PP_SMPL_DCSN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PP_SMPL_FILE_RND_ID",
                schema: "PROD",
                table: "PP_SMPL_FILE",
                column: "RND_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PP_SMPL_RND_PP_SMPL_DCSN_ID",
                schema: "PROD",
                table: "PP_SMPL_RND",
                column: "PP_SMPL_DCSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PP_SMPL_RND_RUN_ID_RND_NO",
                schema: "PROD",
                table: "PP_SMPL_RND",
                columns: new[] { "RUN_ID", "RND_NO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PROD_EXPC_HIST_RUN_ID",
                schema: "PROD",
                table: "PROD_EXPC_HIST",
                column: "RUN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROD_MLST_MLST_TYP_ID",
                schema: "PROD",
                table: "PROD_MLST",
                column: "MLST_TYP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROD_MLST_PP_RND_ID",
                schema: "PROD",
                table: "PROD_MLST",
                column: "PP_RND_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROD_MLST_RUN_ID",
                schema: "PROD",
                table: "PROD_MLST",
                column: "RUN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROD_MLST_FNSH_LINE_MLST_ID_SIZE_ID_CLR_ID",
                schema: "PROD",
                table: "PROD_MLST_FNSH_LINE",
                columns: new[] { "MLST_ID", "SIZE_ID", "CLR_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PROD_RUN_MAIN_EXPC_CMPL_DT",
                schema: "PROD",
                table: "PROD_RUN_MAIN",
                column: "EXPC_CMPL_DT");

            migrationBuilder.CreateIndex(
                name: "IX_PROD_RUN_MAIN_PO_ID",
                schema: "PROD",
                table: "PROD_RUN_MAIN",
                column: "PO_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PROD_RUN_MAIN_PROD_RUN_STS_ID",
                schema: "PROD",
                table: "PROD_RUN_MAIN",
                column: "PROD_RUN_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PROD_RUN_STS_LKP_CODE",
                schema: "PROD",
                table: "PROD_RUN_STS_LKP",
                column: "CODE",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DLVR_NOTE_LINE",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "INBX",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "PP_SMPL_FILE",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "PROD_EXPC_HIST",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "PROD_MLST_FNSH_LINE",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "DLVR_NOTE",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "PROD_MLST",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "DLVR_QTY_STS_LKP",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "DLVR_TMNG_LKP",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "MLST_TYP_LKP",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "PP_SMPL_RND",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "PP_SMPL_DCSN_LKP",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "PROD_RUN_MAIN",
                schema: "PROD");

            migrationBuilder.DropTable(
                name: "PROD_RUN_STS_LKP",
                schema: "PROD");
        }
    }
}
