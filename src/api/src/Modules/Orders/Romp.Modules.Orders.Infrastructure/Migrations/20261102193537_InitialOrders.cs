using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

namespace Romp.Modules.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ORDR");

            migrationBuilder.CreateTable(
                name: "ACTR_KIND_LKP",
                schema: "ORDR",
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
                    table.PrimaryKey("PK_ACTR_KIND_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CNCL_RSN_LKP",
                schema: "ORDR",
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
                    table.PrimaryKey("PK_CNCL_RSN_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "INBX",
                schema: "ORDR",
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
                name: "ORDR_NO_SEQ",
                schema: "ORDR",
                columns: table => new
                {
                    YR = table.Column<short>(type: "smallint", nullable: false),
                    SEQ = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORDR_NO_SEQ", x => x.YR);
                });

            migrationBuilder.CreateTable(
                name: "ORDR_STS_LKP",
                schema: "ORDR",
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
                    table.PrimaryKey("PK_ORDR_STS_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "OUTB_MSG",
                schema: "ORDR",
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
                name: "PAYM_METH_LKP",
                schema: "ORDR",
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
                    table.PrimaryKey("PK_PAYM_METH_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ORDR_MAIN",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_NO = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PLCE_KEY = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CUST_ID = table.Column<long>(type: "bigint", nullable: true),
                    ORDR_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    PAYM_METH_ID = table.Column<short>(type: "smallint", nullable: false),
                    CART_REF = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CNTC_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CNTC_EML = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    CNTC_PHON = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WHTS_OPTN_IND = table.Column<bool>(type: "boolean", nullable: false),
                    WHTS_OPTN_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RCPT_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RCPT_PHON = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LINE_ONE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LINE_TWO = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CITY_ID = table.Column<short>(type: "smallint", nullable: false),
                    CITY_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PROV_ID = table.Column<short>(type: "smallint", nullable: false),
                    PROV_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PCDE = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DLVR_NOTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SUBT_AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DISC_AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SHPG_AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TOT_AMT = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DISC_CODE = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PLCD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORDR_MAIN", x => x.ID);
                    table.CheckConstraint("CK_ORDR_MAIN_AMTS", "\"SUBT_AMT\" > 0 AND \"DISC_AMT\" >= 0 AND \"SHPG_AMT\" >= 0 AND \"DISC_AMT\" <= \"SUBT_AMT\" AND \"TOT_AMT\" = \"SUBT_AMT\" - \"DISC_AMT\" + \"SHPG_AMT\"");
                    table.ForeignKey(
                        name: "FK_ORDR_MAIN_ORDR_STS_LKP",
                        column: x => x.ORDR_STS_ID,
                        principalSchema: "ORDR",
                        principalTable: "ORDR_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORDR_MAIN_PAYM_METH_LKP",
                        column: x => x.PAYM_METH_ID,
                        principalSchema: "ORDR",
                        principalTable: "PAYM_METH_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ORDR_HIST",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    FROM_STS_ID = table.Column<short>(type: "smallint", nullable: true),
                    TO_STS_ID = table.Column<short>(type: "smallint", nullable: false),
                    CNCL_RSN_ID = table.Column<short>(type: "smallint", nullable: true),
                    ACTR_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    ACTR_REF = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NOTE = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OCCR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORDR_HIST", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ORDR_HIST_ACTR_KIND",
                        column: x => x.ACTR_KIND_ID,
                        principalSchema: "ORDR",
                        principalTable: "ACTR_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORDR_HIST_CNCL_RSN",
                        column: x => x.CNCL_RSN_ID,
                        principalSchema: "ORDR",
                        principalTable: "CNCL_RSN_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORDR_HIST_FROM_STS",
                        column: x => x.FROM_STS_ID,
                        principalSchema: "ORDR",
                        principalTable: "ORDR_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORDR_HIST_ORDR_MAIN",
                        column: x => x.ORDR_ID,
                        principalSchema: "ORDR",
                        principalTable: "ORDR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ORDR_HIST_TO_STS",
                        column: x => x.TO_STS_ID,
                        principalSchema: "ORDR",
                        principalTable: "ORDR_STS_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ORDR_LINE",
                schema: "ORDR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORDR_ID = table.Column<long>(type: "bigint", nullable: false),
                    VRNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    PRDT_ID = table.Column<long>(type: "bigint", nullable: false),
                    SKU = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CLR_NAME = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SIZE_NAME = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UNIT_PRIC = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORDR_LINE", x => x.ID);
                    table.CheckConstraint("CK_ORDR_LINE_QTY_PRIC", "\"QTY\" > 0 AND \"UNIT_PRIC\" > 0");
                    table.ForeignKey(
                        name: "FK_ORDR_LINE_ORDR_MAIN",
                        column: x => x.ORDR_ID,
                        principalSchema: "ORDR",
                        principalTable: "ORDR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "ORDR",
                table: "ACTR_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "STAF", "A member of staff", true, "Staff", (short)1 },
                    { (short)2, "CUST", "The customer", true, "Customer", (short)2 },
                    { (short)3, "SYST", "A background job or rule", true, "System", (short)3 },
                    { (short)4, "GWAY", "The payment gateway", true, "Gateway", (short)4 }
                });

            migrationBuilder.InsertData(
                schema: "ORDR",
                table: "CNCL_RSN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "STAF", "Cancelled by a member of staff", true, "Staff", (short)1 },
                    { (short)2, "CODN", "Cash on delivery: nobody answered the confirmation calls", true, "No answer", (short)2 },
                    { (short)3, "CODR", "Cash on delivery: the customer refused the order", true, "Refused", (short)3 },
                    { (short)4, "CUST", "Cancelled by the customer", true, "Customer", (short)4 },
                    { (short)5, "PYFL", "The online payment failed", true, "Payment failed", (short)5 },
                    { (short)6, "PYEX", "The payment window passed without payment", true, "Payment expired", (short)6 }
                });

            migrationBuilder.InsertData(
                schema: "ORDR",
                table: "ORDR_STS_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "PNDC", "Cash on delivery: waiting for the customer to be confirmed", true, "Pending Confirmation", (short)1 },
                    { (short)2, "CNFD", "Confirmed and ready to be fulfilled", true, "Confirmed", (short)2 },
                    { (short)3, "PYPN", "Online payment has not completed yet", true, "Payment Pending", (short)3 },
                    { (short)4, "PAID", "Paid online", true, "Paid", (short)4 },
                    { (short)5, "CNCL", "Cancelled; stock and discount are given back", true, "Cancelled", (short)5 },
                    { (short)6, "DSPT", "Handed to the courier", true, "Dispatched", (short)6 },
                    { (short)7, "DLVD", "Delivered to the customer", true, "Delivered", (short)7 }
                });

            migrationBuilder.InsertData(
                schema: "ORDR",
                table: "PAYM_METH_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "COD", "Pay the courier when the parcel arrives", true, "Cash on Delivery", (short)1 },
                    { (short)2, "EASY", "Pay online with Easypaisa", true, "Easypaisa", (short)2 },
                    { (short)3, "JAZZ", "Pay online with JazzCash", true, "JazzCash", (short)3 },
                    { (short)4, "CARD", "Pay online with a debit or credit card", true, "Card", (short)4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACTR_KIND_LKP_CODE",
                schema: "ORDR",
                table: "ACTR_KIND_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CNCL_RSN_LKP_CODE",
                schema: "ORDR",
                table: "CNCL_RSN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_HIST_ACTR_KIND_ID",
                schema: "ORDR",
                table: "ORDR_HIST",
                column: "ACTR_KIND_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_HIST_CNCL_RSN_ID",
                schema: "ORDR",
                table: "ORDR_HIST",
                column: "CNCL_RSN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_HIST_FROM_STS_ID",
                schema: "ORDR",
                table: "ORDR_HIST",
                column: "FROM_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_HIST_ORDR_ID_ID",
                schema: "ORDR",
                table: "ORDR_HIST",
                columns: new[] { "ORDR_ID", "ID" });

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_HIST_TO_STS_ID",
                schema: "ORDR",
                table: "ORDR_HIST",
                column: "TO_STS_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_LINE_VRNT_ID",
                schema: "ORDR",
                table: "ORDR_LINE",
                column: "VRNT_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_ORDR_LINE_ORDR_ID_VRNT_ID",
                schema: "ORDR",
                table: "ORDR_LINE",
                columns: new[] { "ORDR_ID", "VRNT_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_MAIN_CNTC_PHON",
                schema: "ORDR",
                table: "ORDR_MAIN",
                column: "CNTC_PHON");

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_MAIN_CUST_ID",
                schema: "ORDR",
                table: "ORDR_MAIN",
                column: "CUST_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_MAIN_ORDR_STS_ID_PLCD_DTE",
                schema: "ORDR",
                table: "ORDR_MAIN",
                columns: new[] { "ORDR_STS_ID", "PLCD_DTE" });

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_MAIN_PAYM_METH_ID",
                schema: "ORDR",
                table: "ORDR_MAIN",
                column: "PAYM_METH_ID");

            migrationBuilder.CreateIndex(
                name: "UQ_ORDR_MAIN_ORDR_NO",
                schema: "ORDR",
                table: "ORDR_MAIN",
                column: "ORDR_NO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ORDR_MAIN_PLCE_KEY",
                schema: "ORDR",
                table: "ORDR_MAIN",
                column: "PLCE_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ORDR_STS_LKP_CODE",
                schema: "ORDR",
                table: "ORDR_STS_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PAYM_METH_LKP_CODE",
                schema: "ORDR",
                table: "PAYM_METH_LKP",
                column: "CODE",
                unique: true);

            // The history of an order is append-only: a correction is a new row (ORD-5), exactly like the stock ledger.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION "ORDR"."FN_ORDR_HIST_NO_CHNG"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'ORDR.ORDR_HIST is append-only: corrections are new rows';
                END;
                $$;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER "TR_ORDR_HIST_NO_CHNG" BEFORE UPDATE OR DELETE ON "ORDR"."ORDR_HIST"
                FOR EACH ROW EXECUTE FUNCTION "ORDR"."FN_ORDR_HIST_NO_CHNG"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER "TR_ORDR_HIST_NO_CHNG" ON "ORDR"."ORDR_HIST";""");
            migrationBuilder.Sql("""DROP FUNCTION "ORDR"."FN_ORDR_HIST_NO_CHNG"();""");

            migrationBuilder.DropTable(
                name: "INBX",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "ORDR_HIST",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "ORDR_LINE",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "ORDR_NO_SEQ",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "ACTR_KIND_LKP",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "CNCL_RSN_LKP",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "ORDR_MAIN",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "ORDR_STS_LKP",
                schema: "ORDR");

            migrationBuilder.DropTable(
                name: "PAYM_METH_LKP",
                schema: "ORDR");
        }
    }
}
