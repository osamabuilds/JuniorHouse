using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Romp.Modules.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SRCH_TEXT",
                schema: "STFR",
                table: "PRDT_MAIN",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SRCH_MISS",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QRY_TEXT = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MISS_DT = table.Column<DateOnly>(type: "date", nullable: false),
                    MISS_CNT = table.Column<int>(type: "integer", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SRCH_MISS", x => x.ID);
                    table.CheckConstraint("CK_SRCH_MISS_CNT_POS", "\"MISS_CNT\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "SRCH_SYNM_GRP",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SRCH_SYNM_GRP", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SRCH_SYNM_TERM",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GRP_ID = table.Column<long>(type: "bigint", nullable: false),
                    TERM = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PHRS_IND = table.Column<bool>(type: "boolean", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SRCH_SYNM_TERM", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SRCH_SYNM_TERM_GRP_ID",
                        column: x => x.GRP_ID,
                        principalSchema: "STFR",
                        principalTable: "SRCH_SYNM_GRP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SRCH_MISS_MISS_DT",
                schema: "STFR",
                table: "SRCH_MISS",
                column: "MISS_DT");

            migrationBuilder.CreateIndex(
                name: "IX_SRCH_MISS_QRY_TEXT_MISS_DT",
                schema: "STFR",
                table: "SRCH_MISS",
                columns: new[] { "QRY_TEXT", "MISS_DT" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SRCH_SYNM_GRP_NAME",
                schema: "STFR",
                table: "SRCH_SYNM_GRP",
                column: "NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SRCH_SYNM_TERM_GRP_ID",
                schema: "STFR",
                table: "SRCH_SYNM_TERM",
                column: "GRP_ID");

            migrationBuilder.CreateIndex(
                name: "IX_SRCH_SYNM_TERM_TERM",
                schema: "STFR",
                table: "SRCH_SYNM_TERM",
                column: "TERM",
                unique: true);

            // Full-text vector over the product row: name weighs most (A), the words from category/collection/colour next (B), the
            // description least (C). A generated column keeps it in step with the row without any application code (search plan, Data).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(@"
ALTER TABLE ""STFR"".""PRDT_MAIN"" ADD COLUMN ""SRCH_VEC"" tsvector GENERATED ALWAYS AS (
    setweight(to_tsvector('simple', ""NAME""), 'A') ||
    setweight(to_tsvector('simple', ""SRCH_TEXT""), 'B') ||
    setweight(to_tsvector('simple', ""DSCR""), 'C')) STORED;");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_PRDT_MAIN_SRCH_VEC"" ON ""STFR"".""PRDT_MAIN"" USING gin (""SRCH_VEC"");");
            migrationBuilder.Sql(@"CREATE INDEX ""IX_PRDT_MAIN_SRCH_TEXT_TRGM"" ON ""STFR"".""PRDT_MAIN"" USING gin (""SRCH_TEXT"" gin_trgm_ops);");

            // Rows projected before this migration get their search text now; later snapshots and rebuilds keep it current.
            migrationBuilder.Sql(@"
UPDATE ""STFR"".""PRDT_MAIN"" p SET ""SRCH_TEXT"" = left(lower(concat_ws(' ',
    p.""NAME"", p.""STYL_CODE"", p.""GNDR_NAME"", p.""CATG_NAME"", p.""AGE_NAME"",
    (SELECT string_agg(c.""COLN_NAME"", ' ') FROM ""STFR"".""PRDT_COLN_MAP"" c WHERE c.""PRDT_ID"" = p.""ID""),
    (SELECT string_agg(DISTINCT v.""CLR_NAME"", ' ') FROM ""STFR"".""PRDT_VRNT"" v WHERE v.""PRDT_ID"" = p.""ID""))), 1000);");

            // Starter synonyms: English and Roman Urdu variants of what Romp sells. The owner extends them from the zero-result log.
            var groups = new (string Name, string[] Terms)[]
            {
                ("dress", new[] { "frock", "dress", "gown" }),
                ("kurta", new[] { "kurta", "kameez", "kurtha" }),
                ("shalwar kameez", new[] { "shalwar kameez", "shalwar qameez", "salwar kameez" }),
                ("jumpsuit", new[] { "jumpsuit", "romper", "dungaree", "dungarees" }),
                ("t-shirt", new[] { "tee", "t-shirt", "tshirt" }),
                ("nightwear", new[] { "pajama", "pyjama", "nightwear" }),
                ("jacket", new[] { "jacket", "coat" }),
            };
            foreach (var (name, terms) in groups)
            {
                migrationBuilder.Sql($@"INSERT INTO ""STFR"".""SRCH_SYNM_GRP"" (""NAME"", ""ACT_IND"", ""INSR_DTE"", ""INSR_BY"") VALUES ('{name}', true, now(), 'seed');");
                foreach (var term in terms)
                {
                    migrationBuilder.Sql($@"INSERT INTO ""STFR"".""SRCH_SYNM_TERM"" (""GRP_ID"", ""TERM"", ""PHRS_IND"", ""INSR_DTE"", ""INSR_BY"")
SELECT g.""ID"", '{term}', {(term.Contains(' ') ? "true" : "false")}, now(), 'seed' FROM ""STFR"".""SRCH_SYNM_GRP"" g WHERE g.""NAME"" = '{name}';");
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""STFR"".""IX_PRDT_MAIN_SRCH_TEXT_TRGM"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""STFR"".""IX_PRDT_MAIN_SRCH_VEC"";");
            migrationBuilder.Sql(@"ALTER TABLE ""STFR"".""PRDT_MAIN"" DROP COLUMN IF EXISTS ""SRCH_VEC"";");

            migrationBuilder.DropTable(
                name: "SRCH_MISS",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "SRCH_SYNM_TERM",
                schema: "STFR");

            migrationBuilder.DropTable(
                name: "SRCH_SYNM_GRP",
                schema: "STFR");

            migrationBuilder.DropColumn(
                name: "SRCH_TEXT",
                schema: "STFR",
                table: "PRDT_MAIN");
        }
    }
}
