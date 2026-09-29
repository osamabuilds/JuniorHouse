using Microsoft.EntityFrameworkCore.Migrations;
using Romp.Modules.Vendor.Domain.Vendors;

#nullable disable

namespace Romp.Modules.Vendor.Infrastructure.Migrations
{
    /// <summary>
    /// SCRUM-93 task 21 (AC-64): backfills a synthetic Rev 0 for every existing Sprint 1 PO that has
    /// ever been Sent (POs that never left Draft get none), built from the PO's current terms/lines,
    /// immediately In-force (STS_ID = 2). POs that were also ever Acknowledged additionally get an
    /// acknowledged-revision-0 PO_VNDR_COMM record (Confirmed, channel Unspecified). Lookup IDs are
    /// the seeded values from the Reference module's Sprint2Lookups migration: AMND_INIT_LKP.Buyer=1,
    /// AMND_RSN_LKP.Other=11, PO_VNDR_COMM_TYP_LKP.Confirmed=1, VNDR_COMM_CHNL_LKP.Unspecified=5,
    /// PO_REV_STS_LKP.InForce=2.
    /// </summary>
    public partial class BackfillPoRevisionZero : Migration
    {
        private const string MigrationActor = "scrum-93-task21-migration";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                INSERT INTO "VNDR"."PO_REV"
                    ("PO_ID", "REV_NO", "INIT_ID", "STS_ID", "RSN_ID", "IMPC_NOTE", "VNDR_MSG",
                     "UNIT_COST_AMT", "EXPC_DLVR_DT", "LATE_ACPT_DT", "OVER_TOL_PCT", "UNDR_TOL_PCT", "PAYM_TERM_ID", "ADV_PCT", "FBRC_RESP_ID",
                     "PO_VAL_BEF_AMT", "PO_VAL_AFT_AMT", "PO_VAL_DIFF_AMT", "ADV_AMT_BEF", "ADV_AMT_AFT",
                     "EXPC_DT_SHFT_DAY", "LATE_ACPT_DT_SHFT_DAY", "QTY_DIFF", "BYND_LATE_IND",
                     "INSR_DTE", "INSR_BY")
                SELECT
                    po."ID", 0, 1, 2, 11,
                    'Backfilled Rev 0 from Sprint 1 data (SCRUM-93 task 21, AC-64).', NULL,
                    po."UNIT_COST_AMT", po."EXPC_DLVR_DT", po."LATE_ACPT_DT", po."OVER_TOL_PCT", po."UNDR_TOL_PCT", po."PAYM_TERM_ID", po."ADV_PCT", po."FBRC_RESP_ID",
                    poval."VAL", poval."VAL", 0,
                    poval."VAL" * po."ADV_PCT" / 100, poval."VAL" * po."ADV_PCT" / 100,
                    0, NULL, 0,
                    (po."LATE_ACPT_DT" IS NOT NULL AND po."EXPC_DLVR_DT" > po."LATE_ACPT_DT"),
                    now(), '{MigrationActor}'
                FROM "VNDR"."PO_MAIN" po
                CROSS JOIN LATERAL (
                    SELECT COALESCE(SUM(l."QTY"), 0) * po."UNIT_COST_AMT" AS "VAL"
                    FROM "VNDR"."PO_LINE" l
                    WHERE l."PO_ID" = po."ID"
                ) poval
                WHERE EXISTS (
                    SELECT 1 FROM "VNDR"."PO_STS_HIST" h WHERE h."PO_ID" = po."ID" AND h."PO_STS_ID" = 2
                )
                """);

            migrationBuilder.Sql(
                $"""
                INSERT INTO "VNDR"."PO_REV_STS_HIST" ("PO_REV_ID", "FROM_STS_ID", "TO_STS_ID", "NOTE", "INSR_DTE", "INSR_BY")
                SELECT r."ID", NULL, 2, 'Backfilled', now(), '{MigrationActor}'
                FROM "VNDR"."PO_REV" r
                WHERE r."REV_NO" = 0 AND r."INSR_BY" = '{MigrationActor}'
                """);

            migrationBuilder.Sql(
                $"""
                INSERT INTO "VNDR"."PO_REV_LINE" ("PO_REV_ID", "SIZE_ID", "CLR_ID", "QTY", "INSR_DTE", "INSR_BY")
                SELECT r."ID", l."SIZE_ID", l."CLR_ID", l."QTY", now(), '{MigrationActor}'
                FROM "VNDR"."PO_REV" r
                JOIN "VNDR"."PO_LINE" l ON l."PO_ID" = r."PO_ID"
                WHERE r."REV_NO" = 0 AND r."INSR_BY" = '{MigrationActor}'
                """);

            migrationBuilder.Sql(
                $"""
                INSERT INTO "VNDR"."PO_VNDR_COMM" ("PO_ID", "PO_REV_ID", "COMM_TYP_ID", "CHNL_ID", "RSPR_NAME", "RSPN_DTE", "INSR_DTE", "INSR_BY")
                SELECT r."PO_ID", r."ID", 1, 5, 'Unspecified', now(), now(), '{MigrationActor}'
                FROM "VNDR"."PO_REV" r
                WHERE r."REV_NO" = 0 AND r."INSR_BY" = '{MigrationActor}'
                  AND EXISTS (SELECT 1 FROM "VNDR"."PO_STS_HIST" h WHERE h."PO_ID" = r."PO_ID" AND h."PO_STS_ID" = 3)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""DELETE FROM "VNDR"."PO_VNDR_COMM" WHERE "INSR_BY" = '{MigrationActor}'""");
            migrationBuilder.Sql(
                $"""DELETE FROM "VNDR"."PO_REV_LINE" WHERE "INSR_BY" = '{MigrationActor}'""");
            migrationBuilder.Sql(
                $"""DELETE FROM "VNDR"."PO_REV_STS_HIST" WHERE "INSR_BY" = '{MigrationActor}'""");
            migrationBuilder.Sql(
                $"""DELETE FROM "VNDR"."PO_REV" WHERE "INSR_BY" = '{MigrationActor}'""");
        }
    }
}
