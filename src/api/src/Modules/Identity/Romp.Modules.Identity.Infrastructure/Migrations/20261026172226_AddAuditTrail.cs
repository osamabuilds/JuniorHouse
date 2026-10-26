using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Avoid constant arrays as arguments

namespace Romp.Modules.Identity.Infrastructure.Migrations
{
    /// <summary>
    /// The audit trail store (identity/audit-trail, ADR 0016). The lookup is ordinary; AUDT_ENTY and AUDT_CHNG are written in raw SQL
    /// (every identifier quoted) because EF cannot express what they need: monthly range partitions on OCCR_DTE, a function that creates
    /// them, and triggers that reject UPDATE, DELETE and TRUNCATE so the trail is append-only at the database level (AC-4).
    /// </summary>
    public partial class AddAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AUDT_OPRN_LKP",
                schema: "IDNT",
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
                    table.PrimaryKey("PK_AUDT_OPRN_LKP", x => x.ID);
                });

            migrationBuilder.InsertData(
                schema: "IDNT",
                table: "AUDT_OPRN_LKP",
                columns: new[] { "ID", "CODE", "DSCR", "ACT_IND", "NAME", "SORT_SEQ" },
                values: new object[,]
                {
                    { (short)1, "INSERT", null, true, "Created", (short)1 },
                    { (short)2, "UPDATE", null, true, "Changed", (short)2 },
                    { (short)3, "DELETE", null, true, "Deleted", (short)3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AUDT_OPRN_LKP_CODE",
                schema: "IDNT",
                table: "AUDT_OPRN_LKP",
                column: "CODE",
                unique: true);

            migrationBuilder.Sql("""
                CREATE SEQUENCE "IDNT"."AUDT_ENTY_ID_SEQ";
                CREATE SEQUENCE "IDNT"."AUDT_CHNG_ID_SEQ";

                CREATE TABLE "IDNT"."AUDT_ENTY" (
                    "ID" bigint NOT NULL DEFAULT nextval('"IDNT"."AUDT_ENTY_ID_SEQ"'),
                    "OCCR_DTE" timestamp with time zone NOT NULL,
                    "MODL_NAME" character varying(20) NOT NULL,
                    "SRC_MSG_ID" bigint NOT NULL,
                    "ENTY_TYP_NAME" character varying(100) NOT NULL,
                    "ENTY_KEY" character varying(200) NOT NULL,
                    "OPRN_ID" smallint NOT NULL,
                    "ACTR_NAME" character varying(100) NOT NULL,
                    "CORR_ID" character varying(64) NULL,
                    CONSTRAINT "PK_AUDT_ENTY" PRIMARY KEY ("ID", "OCCR_DTE"),
                    CONSTRAINT "FK_AUDT_ENTY_OPRN_ID" FOREIGN KEY ("OPRN_ID") REFERENCES "IDNT"."AUDT_OPRN_LKP" ("ID") ON DELETE RESTRICT
                ) PARTITION BY RANGE ("OCCR_DTE");

                CREATE TABLE "IDNT"."AUDT_CHNG" (
                    "ID" bigint NOT NULL DEFAULT nextval('"IDNT"."AUDT_CHNG_ID_SEQ"'),
                    "OCCR_DTE" timestamp with time zone NOT NULL,
                    "ENTY_ID" bigint NOT NULL,
                    "COLM_NAME" character varying(100) NOT NULL,
                    CONSTRAINT "PK_AUDT_CHNG" PRIMARY KEY ("ID", "OCCR_DTE"),
                    CONSTRAINT "FK_AUDT_CHNG_ENTY" FOREIGN KEY ("ENTY_ID", "OCCR_DTE") REFERENCES "IDNT"."AUDT_ENTY" ("ID", "OCCR_DTE") ON DELETE RESTRICT
                ) PARTITION BY RANGE ("OCCR_DTE");

                ALTER SEQUENCE "IDNT"."AUDT_ENTY_ID_SEQ" OWNED BY "IDNT"."AUDT_ENTY"."ID";
                ALTER SEQUENCE "IDNT"."AUDT_CHNG_ID_SEQ" OWNED BY "IDNT"."AUDT_CHNG"."ID";

                CREATE UNIQUE INDEX "UX_AUDT_CHNG_COLM" ON "IDNT"."AUDT_CHNG" ("ENTY_ID", "OCCR_DTE", "COLM_NAME");
                CREATE INDEX "IX_AUDT_ENTY_ACTR_NAME" ON "IDNT"."AUDT_ENTY" ("ACTR_NAME", "OCCR_DTE");
                CREATE INDEX "IX_AUDT_ENTY_ENTY_KEY" ON "IDNT"."AUDT_ENTY" ("ENTY_TYP_NAME", "ENTY_KEY", "OCCR_DTE");
                CREATE INDEX "IX_AUDT_ENTY_MODL_NAME" ON "IDNT"."AUDT_ENTY" ("MODL_NAME", "OCCR_DTE");
                CREATE INDEX "IX_AUDT_ENTY_OCCR_DTE" ON "IDNT"."AUDT_ENTY" ("OCCR_DTE");
                CREATE INDEX "IX_AUDT_ENTY_OPRN_ID" ON "IDNT"."AUDT_ENTY" ("OPRN_ID");
                CREATE UNIQUE INDEX "UX_AUDT_ENTY_SRC" ON "IDNT"."AUDT_ENTY" ("MODL_NAME", "SRC_MSG_ID", "ENTY_TYP_NAME", "ENTY_KEY", "OCCR_DTE");
                """);

            // Append-only at the database level (AC-4): no UPDATE, DELETE or TRUNCATE on the trail, whoever asks.
            migrationBuilder.Sql("""
                CREATE FUNCTION "IDNT"."AUDT_BLOK_CHNG"() RETURNS trigger LANGUAGE plpgsql AS $fn$
                BEGIN
                    RAISE EXCEPTION 'The audit trail is append-only: % on % is not allowed.', TG_OP, TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END
                $fn$;

                CREATE TRIGGER "AUDT_ENTY_BLOK_ROW" BEFORE UPDATE OR DELETE ON "IDNT"."AUDT_ENTY"
                    FOR EACH ROW EXECUTE FUNCTION "IDNT"."AUDT_BLOK_CHNG"();
                CREATE TRIGGER "AUDT_CHNG_BLOK_ROW" BEFORE UPDATE OR DELETE ON "IDNT"."AUDT_CHNG"
                    FOR EACH ROW EXECUTE FUNCTION "IDNT"."AUDT_BLOK_CHNG"();
                CREATE TRIGGER "AUDT_ENTY_BLOK_TRNC" BEFORE TRUNCATE ON "IDNT"."AUDT_ENTY"
                    FOR EACH STATEMENT EXECUTE FUNCTION "IDNT"."AUDT_BLOK_CHNG"();
                CREATE TRIGGER "AUDT_CHNG_BLOK_TRNC" BEFORE TRUNCATE ON "IDNT"."AUDT_CHNG"
                    FOR EACH STATEMENT EXECUTE FUNCTION "IDNT"."AUDT_BLOK_CHNG"();
                """);

            // Creates the monthly partitions (UTC months) of both tables, and the TRUNCATE guard on each partition (statement triggers are
            // not inherited). Safe to call any time: existing partitions are left alone, and an advisory lock stops two callers racing.
            migrationBuilder.Sql("""
                CREATE FUNCTION "IDNT"."AUDT_PART_ENSR"(first_month date, month_cnt integer) RETURNS void LANGUAGE plpgsql AS $fn$
                DECLARE
                    m date;
                    suffix text;
                    lo timestamptz;
                    hi timestamptz;
                    parent text;
                    child text;
                BEGIN
                    PERFORM pg_advisory_xact_lock(hashtext('IDNT.AUDT_PART_ENSR'));
                    FOR i IN 0..month_cnt - 1 LOOP
                        m := (date_trunc('month', first_month::timestamp) + make_interval(months => i))::date;
                        suffix := to_char(m, 'YYYYMM');
                        lo := m::timestamp AT TIME ZONE 'UTC';
                        hi := (m + interval '1 month')::timestamp AT TIME ZONE 'UTC';
                        FOREACH parent IN ARRAY ARRAY['AUDT_ENTY', 'AUDT_CHNG'] LOOP
                            child := parent || '_' || suffix;
                            IF to_regclass(format('"IDNT".%I', child)) IS NULL THEN
                                EXECUTE format('CREATE TABLE "IDNT".%I PARTITION OF "IDNT".%I FOR VALUES FROM (%L) TO (%L)', child, parent, lo, hi);
                                EXECUTE format('CREATE TRIGGER %I BEFORE TRUNCATE ON "IDNT".%I FOR EACH STATEMENT EXECUTE FUNCTION "IDNT"."AUDT_BLOK_CHNG"()', child || '_TRNC', child);
                            END IF;
                        END LOOP;
                    END LOOP;
                END
                $fn$;

                SELECT "IDNT"."AUDT_PART_ENSR"(date_trunc('month', (now() AT TIME ZONE 'UTC') - interval '1 month')::date, 6);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION "IDNT"."AUDT_PART_ENSR"(date, integer);
                DROP TABLE "IDNT"."AUDT_CHNG";
                DROP TABLE "IDNT"."AUDT_ENTY";
                DROP FUNCTION "IDNT"."AUDT_BLOK_CHNG"();
                DROP SEQUENCE IF EXISTS "IDNT"."AUDT_CHNG_ID_SEQ";
                DROP SEQUENCE IF EXISTS "IDNT"."AUDT_ENTY_ID_SEQ";
                """);

            migrationBuilder.DropTable(
                name: "AUDT_OPRN_LKP",
                schema: "IDNT");
        }
    }
}
