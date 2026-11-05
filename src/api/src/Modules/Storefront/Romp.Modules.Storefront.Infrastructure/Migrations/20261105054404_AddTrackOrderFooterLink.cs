using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Romp.Modules.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackOrderFooterLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Track order" (orders/order-lookup) goes in the Help column just before WhatsApp. Safe on a database the owner has already edited:
            // it only runs when the Help column exists and has no such link, takes the next free id (the seed used fixed ids, so the identity
            // counter cannot be trusted) and moves later links down by one. The settings version is bumped so caches refresh.
            migrationBuilder.Sql("""
                DO $$
                DECLARE col bigint; pos smallint;
                BEGIN
                    SELECT "ID" INTO col FROM "STFR"."FOOT_COLM" WHERE "TTL" = 'Help' ORDER BY "SORT_SEQ" LIMIT 1;
                    IF col IS NULL OR EXISTS (SELECT 1 FROM "STFR"."FOOT_LINK" WHERE "TGT_REF" = '/track-order') THEN
                        RETURN;
                    END IF;
                    SELECT COALESCE(MIN("SORT_SEQ"), 0) INTO pos FROM "STFR"."FOOT_LINK" WHERE "FOOT_COLM_ID" = col AND "LINK_TGT_ID" = 5;
                    IF pos = 0 THEN
                        SELECT COALESCE(MAX("SORT_SEQ"), 0) + 1 INTO pos FROM "STFR"."FOOT_LINK" WHERE "FOOT_COLM_ID" = col;
                    ELSE
                        UPDATE "STFR"."FOOT_LINK" SET "SORT_SEQ" = "SORT_SEQ" + 1 WHERE "FOOT_COLM_ID" = col AND "SORT_SEQ" >= pos;
                    END IF;
                    INSERT INTO "STFR"."FOOT_LINK" ("ID","FOOT_COLM_ID","LBL","LINK_TGT_ID","TGT_REF","SORT_SEQ","ACT_IND","INSR_DTE","INSR_BY")
                    SELECT COALESCE(MAX("ID"), 0) + 1, col, 'Track order', 1, '/track-order', pos, true, now(), 'migration' FROM "STFR"."FOOT_LINK";
                    UPDATE "STFR"."DRFT_MAIN" SET "VER_NO" = "VER_NO" + 1, "UPDT_DTE" = now(), "UPDT_BY" = 'migration';
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "STFR"."FOOT_LINK" WHERE "TGT_REF" = '/track-order' AND "INSR_BY" = 'migration';""");
        }
    }
}
