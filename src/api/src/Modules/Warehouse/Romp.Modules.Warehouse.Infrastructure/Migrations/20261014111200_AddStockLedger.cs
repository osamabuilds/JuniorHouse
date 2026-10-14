using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Romp.Modules.Warehouse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GRD_LKP",
                schema: "WHSE",
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
                    table.PrimaryKey("PK_GRD_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MVMT_KIND_LKP",
                schema: "WHSE",
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
                    table.PrimaryKey("PK_MVMT_KIND_LKP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "VRNT_REF",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VRNT_ID = table.Column<long>(type: "bigint", nullable: false),
                    STYL_ID = table.Column<long>(type: "bigint", nullable: false),
                    CLR_ID = table.Column<short>(type: "smallint", nullable: false),
                    SIZE_ID = table.Column<short>(type: "smallint", nullable: false),
                    SKU_CODE = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    GTIN = table.Column<string>(type: "character(13)", fixedLength: true, maxLength: 13, nullable: false),
                    GTIN_RETD_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VRNT_REF", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "STCK_BAL",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VRNT_REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    GRD_ID = table.Column<short>(type: "smallint", nullable: false),
                    BIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    QTY = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STCK_BAL", x => x.ID);
                    table.CheckConstraint("CK_STCK_BAL_QTY_NONNEG", "\"QTY\" >= 0");
                    table.ForeignKey(
                        name: "FK_STCK_BAL_BIN_MAIN",
                        column: x => x.BIN_ID,
                        principalSchema: "WHSE",
                        principalTable: "BIN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_STCK_BAL_GRD_LKP",
                        column: x => x.GRD_ID,
                        principalSchema: "WHSE",
                        principalTable: "GRD_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_STCK_BAL_VRNT_REF",
                        column: x => x.VRNT_REF_ID,
                        principalSchema: "WHSE",
                        principalTable: "VRNT_REF",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "STCK_MVMT",
                schema: "WHSE",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VRNT_REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    GRD_ID = table.Column<short>(type: "smallint", nullable: false),
                    BIN_ID = table.Column<long>(type: "bigint", nullable: false),
                    MVMT_KIND_ID = table.Column<short>(type: "smallint", nullable: false),
                    REF_ID = table.Column<long>(type: "bigint", nullable: false),
                    QTY_CHNG = table.Column<int>(type: "integer", nullable: false),
                    BEF_QTY = table.Column<int>(type: "integer", nullable: false),
                    AFT_QTY = table.Column<int>(type: "integer", nullable: false),
                    ACTR_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_STCK_MVMT", x => x.ID);
                    table.CheckConstraint("CK_STCK_MVMT_BALANCES", "\"AFT_QTY\" = \"BEF_QTY\" + \"QTY_CHNG\" AND \"AFT_QTY\" >= 0");
                    table.CheckConstraint("CK_STCK_MVMT_QTY_CHNG_NONZERO", "\"QTY_CHNG\" <> 0");
                    table.ForeignKey(
                        name: "FK_STCK_MVMT_BIN_MAIN",
                        column: x => x.BIN_ID,
                        principalSchema: "WHSE",
                        principalTable: "BIN_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_STCK_MVMT_GRD_LKP",
                        column: x => x.GRD_ID,
                        principalSchema: "WHSE",
                        principalTable: "GRD_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_STCK_MVMT_MVMT_KIND_LKP",
                        column: x => x.MVMT_KIND_ID,
                        principalSchema: "WHSE",
                        principalTable: "MVMT_KIND_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_STCK_MVMT_VRNT_REF",
                        column: x => x.VRNT_REF_ID,
                        principalSchema: "WHSE",
                        principalTable: "VRNT_REF",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "WHSE",
                table: "GRD_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "ACCEPTED", "Accepted by QC", true, "Accepted", (short)1 },
                    { (short)2, "CONCESSION", "Accepted with concession (2ND mark)", true, "Concession", (short)2 }
                });

            migrationBuilder.InsertData(
                schema: "WHSE",
                table: "MVMT_KIND_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "PUTAWAY", "Stock shelved by scan", true, "Putaway", (short)1 },
                    { (short)2, "ADJUSTMENT", "Reason-coded correction", true, "Adjustment", (short)2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_GRD_LKP_CODE",
                schema: "WHSE",
                table: "GRD_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MVMT_KIND_LKP_CODE",
                schema: "WHSE",
                table: "MVMT_KIND_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_STCK_BAL_BIN_ID",
                schema: "WHSE",
                table: "STCK_BAL",
                column: "BIN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STCK_BAL_GRD_ID",
                schema: "WHSE",
                table: "STCK_BAL",
                column: "GRD_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STCK_BAL_VRNT_REF_ID_GRD_ID_BIN_ID",
                schema: "WHSE",
                table: "STCK_BAL",
                columns: new[] { "VRNT_REF_ID", "GRD_ID", "BIN_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_STCK_MVMT_BIN_ID_ID",
                schema: "WHSE",
                table: "STCK_MVMT",
                columns: new[] { "BIN_ID", "ID" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_STCK_MVMT_GRD_ID",
                schema: "WHSE",
                table: "STCK_MVMT",
                column: "GRD_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STCK_MVMT_MVMT_KIND_ID",
                schema: "WHSE",
                table: "STCK_MVMT",
                column: "MVMT_KIND_ID");

            migrationBuilder.CreateIndex(
                name: "IX_STCK_MVMT_VRNT_REF_ID_ID",
                schema: "WHSE",
                table: "STCK_MVMT",
                columns: new[] { "VRNT_REF_ID", "ID" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_REF_GTIN",
                schema: "WHSE",
                table: "VRNT_REF",
                column: "GTIN");

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_REF_STYL_ID_CLR_ID_SIZE_ID",
                schema: "WHSE",
                table: "VRNT_REF",
                columns: new[] { "STYL_ID", "CLR_ID", "SIZE_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_VRNT_REF_VRNT_ID",
                schema: "WHSE",
                table: "VRNT_REF",
                column: "VRNT_ID",
                unique: true);

            // ADR 0012, NFR-SC-05: the ledger is append-only. Corrections are new rows, so any UPDATE or DELETE is refused by the database itself.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION "WHSE"."FN_STCK_MVMT_NO_CHNG"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'WHSE.STCK_MVMT is append-only: corrections are new rows';
                END;
                $$;
                """);
            migrationBuilder.Sql(
                """
                CREATE TRIGGER "TR_STCK_MVMT_NO_CHNG" BEFORE UPDATE OR DELETE ON "WHSE"."STCK_MVMT"
                FOR EACH ROW EXECUTE FUNCTION "WHSE"."FN_STCK_MVMT_NO_CHNG"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER "TR_STCK_MVMT_NO_CHNG" ON "WHSE"."STCK_MVMT";""");
            migrationBuilder.Sql("""DROP FUNCTION "WHSE"."FN_STCK_MVMT_NO_CHNG"();""");

            migrationBuilder.DropTable(
                name: "STCK_BAL",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "STCK_MVMT",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "GRD_LKP",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "MVMT_KIND_LKP",
                schema: "WHSE");

            migrationBuilder.DropTable(
                name: "VRNT_REF",
                schema: "WHSE");
        }
    }
}
