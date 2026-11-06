using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Romp.Modules.Storefront.Infrastructure.Persistence;

#nullable disable

namespace Romp.Modules.Storefront.Infrastructure.Migrations
{
    /// <summary>
    /// Sold items in listings, task 21 (SLD-10): the launch navigation becomes Season &gt; categories with our own labels (Winter and Summer,
    /// each with T-shirts, Sweaters, Jackets, Trousers, Shorts, Dresses), then Girls and Boys with the same categories, New in and Sale. It equals
    /// the storefront's built-in launch set (<c>launch-settings.ts</c>); the contract spec compares the two. Safe on a database the owner has
    /// already edited: it replaces the navigation only while it is still exactly the original launch tree (19 items, none edited), and bumps the
    /// settings version so the draft shows as changed. The live menu changes when the owner publishes.
    /// </summary>
    [DbContext(typeof(StfrDbContext))]
    [Migration("20261106090000_SeedSeasonNavigation")]
    public partial class SeedSeasonNavigation : Migration
    {
        private const string Audit = "now(), 'migration'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM "STFR"."NAV_ITEM") = 19 AND EXISTS (SELECT 1 FROM "STFR"."NAV_ITEM" WHERE "ITEM_KEY" = 'girls-dresses') AND NOT EXISTS (SELECT 1 FROM "STFR"."NAV_ITEM" WHERE "UPDT_BY" IS NOT NULL) THEN
                        DELETE FROM "STFR"."NAV_ITEM";
                        INSERT INTO "STFR"."NAV_ITEM" ("ID","PRNT_ID","ITEM_KEY","LBL","NAV_KIND_ID","LINK_TGT_ID","TGT_REF","SORT_SEQ","ACT_IND","SHOW_AGE_IND","INSR_DTE","INSR_BY") VALUES
                    (1,NULL,'winter','Winter',1,1,'/collections/winter',1,true,false,{Audit}),
                    (2,NULL,'summer','Summer',1,1,'/collections/summer',2,true,false,{Audit}),
                    (3,NULL,'girls','Girls',1,1,'/girls',3,true,true,{Audit}),
                    (4,NULL,'boys','Boys',1,1,'/boys',4,true,true,{Audit}),
                    (5,NULL,'new-in','New in',1,1,'/new-in',5,true,false,{Audit}),
                    (6,NULL,'sale','Sale',1,1,'/sale',6,true,false,{Audit}),
                    (7,1,'winter-t-shirts','T-shirts',1,1,'/collections/winter/t-shirts',1,true,false,{Audit}),
                    (8,1,'winter-sweaters','Sweaters',1,1,'/collections/winter/sweaters',2,true,false,{Audit}),
                    (9,1,'winter-jackets','Jackets',1,1,'/collections/winter/jackets',3,true,false,{Audit}),
                    (10,1,'winter-trousers','Trousers',1,1,'/collections/winter/trousers',4,true,false,{Audit}),
                    (11,1,'winter-shorts','Shorts',1,1,'/collections/winter/shorts',5,true,false,{Audit}),
                    (12,1,'winter-dresses','Dresses',1,1,'/collections/winter/dresses',6,true,false,{Audit}),
                    (13,2,'summer-t-shirts','T-shirts',1,1,'/collections/summer/t-shirts',1,true,false,{Audit}),
                    (14,2,'summer-sweaters','Sweaters',1,1,'/collections/summer/sweaters',2,true,false,{Audit}),
                    (15,2,'summer-jackets','Jackets',1,1,'/collections/summer/jackets',3,true,false,{Audit}),
                    (16,2,'summer-trousers','Trousers',1,1,'/collections/summer/trousers',4,true,false,{Audit}),
                    (17,2,'summer-shorts','Shorts',1,1,'/collections/summer/shorts',5,true,false,{Audit}),
                    (18,2,'summer-dresses','Dresses',1,1,'/collections/summer/dresses',6,true,false,{Audit}),
                    (19,3,'girls-t-shirts','T-shirts',1,1,'/girls/t-shirts',1,true,false,{Audit}),
                    (20,3,'girls-sweaters','Sweaters',1,1,'/girls/sweaters',2,true,false,{Audit}),
                    (21,3,'girls-jackets','Jackets',1,1,'/girls/jackets',3,true,false,{Audit}),
                    (22,3,'girls-trousers','Trousers',1,1,'/girls/trousers',4,true,false,{Audit}),
                    (23,3,'girls-shorts','Shorts',1,1,'/girls/shorts',5,true,false,{Audit}),
                    (24,3,'girls-dresses','Dresses',1,1,'/girls/dresses',6,true,false,{Audit}),
                    (25,4,'boys-t-shirts','T-shirts',1,1,'/boys/t-shirts',1,true,false,{Audit}),
                    (26,4,'boys-sweaters','Sweaters',1,1,'/boys/sweaters',2,true,false,{Audit}),
                    (27,4,'boys-jackets','Jackets',1,1,'/boys/jackets',3,true,false,{Audit}),
                    (28,4,'boys-trousers','Trousers',1,1,'/boys/trousers',4,true,false,{Audit}),
                    (29,4,'boys-shorts','Shorts',1,1,'/boys/shorts',5,true,false,{Audit});
                        PERFORM setval(pg_get_serial_sequence('"STFR"."NAV_ITEM"', 'ID'), (SELECT MAX("ID") FROM "STFR"."NAV_ITEM"));
                        UPDATE "STFR"."DRFT_MAIN" SET "VER_NO" = "VER_NO" + 1, "UPDT_DTE" = now(), "UPDT_BY" = 'migration';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DO $$
                BEGIN
                    IF (SELECT count(*) FROM "STFR"."NAV_ITEM") = 29 AND EXISTS (SELECT 1 FROM "STFR"."NAV_ITEM" WHERE "ITEM_KEY" = 'winter-jackets') AND NOT EXISTS (SELECT 1 FROM "STFR"."NAV_ITEM" WHERE "UPDT_BY" IS NOT NULL) THEN
                        DELETE FROM "STFR"."NAV_ITEM";
                        INSERT INTO "STFR"."NAV_ITEM" ("ID","PRNT_ID","ITEM_KEY","LBL","NAV_KIND_ID","LINK_TGT_ID","TGT_REF","SORT_SEQ","ACT_IND","SHOW_AGE_IND","INSR_DTE","INSR_BY") VALUES
                    (1,NULL,'girls','Girls',1,1,'/girls',1,true,true,{Audit}),
                    (2,NULL,'boys','Boys',1,1,'/boys',2,true,true,{Audit}),
                    (3,NULL,'new-in','New in',1,1,'/new-in',3,true,false,{Audit}),
                    (4,NULL,'collections','Collections',3,1,'/collections',4,true,false,{Audit}),
                    (5,NULL,'sale','Sale',1,1,'/sale',5,true,false,{Audit}),
                    (6,1,'girls-dresses','Dresses',1,1,'/girls/dresses',1,true,false,{Audit}),
                    (7,1,'girls-tops','Tops and tees',1,1,'/girls/tops-and-tees',2,true,false,{Audit}),
                    (8,1,'girls-bottoms','Bottoms',1,1,'/girls/bottoms',3,true,false,{Audit}),
                    (9,7,'girls-tees','T-shirts',1,1,'/girls/tops-and-tees/t-shirts',1,true,false,{Audit}),
                    (10,7,'girls-blouses','Blouses',1,1,'/girls/tops-and-tees/blouses',2,true,false,{Audit}),
                    (11,8,'girls-skirts','Skirts',1,1,'/girls/bottoms/skirts',1,true,false,{Audit}),
                    (12,8,'girls-trousers','Trousers',1,1,'/girls/bottoms/trousers',2,true,false,{Audit}),
                    (13,2,'boys-tees','Tees and shirts',1,1,'/boys/tees-and-shirts',1,true,false,{Audit}),
                    (14,2,'boys-bottoms','Bottoms',1,1,'/boys/bottoms',2,true,false,{Audit}),
                    (15,2,'boys-casual','Casual shirts',1,1,'/boys/casual-shirts',3,true,false,{Audit}),
                    (16,13,'boys-t-shirts','T-shirts',1,1,'/boys/tees-and-shirts/t-shirts',1,true,false,{Audit}),
                    (17,13,'boys-polos','Polo shirts',1,1,'/boys/tees-and-shirts/polo-shirts',2,true,false,{Audit}),
                    (18,14,'boys-jeans','Jeans',1,1,'/boys/bottoms/jeans',1,true,false,{Audit}),
                    (19,14,'boys-shorts','Shorts',1,1,'/boys/bottoms/shorts',2,true,false,{Audit});
                        PERFORM setval(pg_get_serial_sequence('"STFR"."NAV_ITEM"', 'ID'), (SELECT MAX("ID") FROM "STFR"."NAV_ITEM"));
                        UPDATE "STFR"."DRFT_MAIN" SET "VER_NO" = "VER_NO" + 1, "UPDT_DTE" = now(), "UPDT_BY" = 'migration';
                    END IF;
                END $$;
                """);
        }
    }
}
