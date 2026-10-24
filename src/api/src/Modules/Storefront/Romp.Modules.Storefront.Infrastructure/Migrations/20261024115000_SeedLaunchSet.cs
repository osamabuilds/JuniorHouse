using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Romp.Modules.Storefront.Infrastructure.Persistence;

#nullable disable

namespace Romp.Modules.Storefront.Infrastructure.Migrations
{
    /// <summary>
    /// The launch set (SCRUM-206 task 5): the navigation (S-1), the ages (S-2), the footer, the brand and WhatsApp, the popular searches and
    /// the eight page drafts with wording only. It equals the storefront's built-in launch set (<c>launch-settings.ts</c>,
    /// <c>launch-pages.ts</c>); a contract spec compares the two. No business fact is seeded (delivery, exchange, email, hours), and every
    /// page is a draft: a page that quotes an empty fact cannot be published (ST-17), so the owner fills the facts first.
    /// </summary>
    [DbContext(typeof(StfrDbContext))]
    [Migration("20261024115000_SeedLaunchSet")]
    public partial class SeedLaunchSet : Migration
    {
        private const string Audit = "now(), 'migration'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO "STFR"."DRFT_MAIN" ("ID","VER_NO","INSR_DTE","INSR_BY") VALUES (1,0,{Audit});
                """);

            // Navigation: kind 1 = link, 3 = collection list; target kind 1 = path. Girls and Boys show the ages column.
            migrationBuilder.Sql($"""
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
                """);

            migrationBuilder.Sql($"""
                INSERT INTO "STFR"."AGE_STEP" ("ID","AGE_CODE","LBL","SORT_SEQ","ACT_IND","INSR_DTE","INSR_BY") VALUES
                (1,'1y','1Y',1,true,{Audit}),(2,'2y','2Y',2,true,{Audit}),(3,'3y','3Y',3,true,{Audit}),(4,'4y','4Y',4,true,{Audit}),
                (5,'5y','5Y',5,true,{Audit}),(6,'6y','6Y',6,true,{Audit}),(7,'7y','7Y',7,true,{Audit}),(8,'8y','8Y',8,true,{Audit});
                """);

            // Footer: link target kind 1 = path, 2 = page, 5 = external.
            migrationBuilder.Sql($"""
                INSERT INTO "STFR"."FOOT_COLM" ("ID","TTL","SORT_SEQ","INSR_DTE","INSR_BY") VALUES
                (1,'Shop',1,{Audit}),(2,'Help',2,{Audit}),(3,'Romp',3,{Audit});
                INSERT INTO "STFR"."FOOT_LINK" ("ID","FOOT_COLM_ID","LBL","LINK_TGT_ID","TGT_REF","SORT_SEQ","ACT_IND","INSR_DTE","INSR_BY") VALUES
                (1,1,'New in',1,'/new-in',1,true,{Audit}),
                (2,1,'Girls',1,'/girls',2,true,{Audit}),
                (3,1,'Boys',1,'/boys',3,true,{Audit}),
                (4,1,'Collections',1,'/collections',4,true,{Audit}),
                (5,1,'Sale',1,'/sale',5,true,{Audit}),
                (6,2,'Delivery',2,'delivery',1,true,{Audit}),
                (7,2,'Exchanges',2,'exchanges',2,true,{Audit}),
                (8,2,'Size guide',2,'size-guide',3,true,{Audit}),
                (9,2,'Contact',2,'contact',4,true,{Audit}),
                (10,2,'WhatsApp',5,'https://wa.me/923315613217',5,true,{Audit}),
                (11,3,'Our story',2,'our-story',1,true,{Audit}),
                (12,3,'Made in Pakistan',2,'made-in-pakistan',2,true,{Audit}),
                (13,3,'Privacy',2,'privacy',3,true,{Audit}),
                (14,3,'Terms',2,'terms',4,true,{Audit});
                """);

            // Brand and contact: keys 1 brand name, 2 tagline, 3 WhatsApp, 6 announcement, 7 announcement shown. Email and hours stay empty.
            migrationBuilder.Sql($"""
                INSERT INTO "STFR"."STNG_ITEM" ("ID","STNG_KEY_ID","VAL","INSR_DTE","INSR_BY") VALUES
                (1,1,'Romp',{Audit}),(2,2,'Children’s clothing',{Audit}),(3,3,'+923315613217',{Audit}),(4,6,'',{Audit}),(5,7,'false',{Audit});
                INSERT INTO "STFR"."POPL_SRCH" ("ID","TEXT","SORT_SEQ","INSR_DTE","INSR_BY") VALUES
                (1,'Dresses',1,{Audit}),(2,'T-shirts',2,{Audit}),(3,'Shorts',3,{Audit});
                """);

            // Pages: all drafts (status 1). Privacy and terms are legal text the owner supplies: no blocks, empty description.
            migrationBuilder.Sql($"""
                INSERT INTO "STFR"."PAGE_MAIN" ("ID","SLUG","TTL","META_DSCR","PAGE_STS_ID","INSR_DTE","INSR_BY") VALUES
                (1,'delivery','Delivery','How your order travels from our hands to yours: where we deliver, how long it takes and what it costs.',1,{Audit}),
                (2,'exchanges','Exchanges','Children grow quickly and sizes can surprise. Here is how exchanges work.',1,{Audit}),
                (3,'size-guide','Size guide','How to measure your child and choose the right size.',1,{Audit}),
                (4,'our-story','Our story','Why we make clothes for children, and how we think about them.',1,{Audit}),
                (5,'made-in-pakistan','Made in Pakistan','How our clothes are made, and the people and standards behind them.',1,{Audit}),
                (6,'contact','Contact','Talk to us about an order, a size or anything else.',1,{Audit}),
                (7,'privacy','Privacy','',1,{Audit}),
                (8,'terms','Terms','',1,{Audit});
                """);

            // Blocks: type 1 heading, 2 paragraph, 3 list, 5 callout, 6 fact. Fact ids: 1 contact.whatsapp, 2 contact.email, 3 contact.hours,
            // 4 exchange.window, 5 exchange.conditions, 6 delivery.rules.
            migrationBuilder.Sql($"""
                INSERT INTO "STFR"."PAGE_BLCK" ("ID","PAGE_MAIN_ID","SORT_SEQ","BLCK_TYP_ID","TEXT","LVL","FACT_ID","LBL","ORDR_IND","INSR_DTE","INSR_BY") VALUES
                (1,1,1,2,'Every order is checked, folded and packed with care before it leaves us. We would rather take a moment longer than send anything out that is not right.',0,NULL,NULL,false,{Audit}),
                (2,1,2,6,'',0,6,'Where we deliver, and how long it takes',false,{Audit}),
                (3,1,3,1,'Tracking and questions',2,NULL,NULL,false,{Audit}),
                (4,1,4,2,'If you would like an update on an order, message us and we will find out for you.',0,NULL,NULL,false,{Audit}),
                (5,1,5,6,'',0,1,'Message us',false,{Audit}),
                (6,2,1,2,'Children grow quickly, and a size that looked right on paper does not always fit on the day. If something is not right, we will help you put it right.',0,NULL,NULL,false,{Audit}),
                (7,2,2,6,'',0,4,'Exchange window',false,{Audit}),
                (8,2,3,6,'',0,5,'Conditions',false,{Audit}),
                (9,2,4,1,'Starting an exchange',2,NULL,NULL,false,{Audit}),
                (10,2,5,2,'Message us with your order number and the item you would like to exchange, and we will take it from there.',0,NULL,NULL,false,{Audit}),
                (11,2,6,6,'',0,1,'Message us',false,{Audit}),
                (12,3,1,2,'The right size is the one that lets a child move, climb and sit comfortably. Measure first, then choose.',0,NULL,NULL,false,{Audit}),
                (13,3,2,1,'How to measure',2,NULL,NULL,false,{Audit}),
                (14,3,3,3,'',0,NULL,NULL,true,{Audit}),
                (15,3,4,1,'Between two sizes',2,NULL,NULL,false,{Audit}),
                (16,3,5,2,'If your child falls between two sizes, choose the larger one: there is room to grow, and sleeves and hems are easy to turn up.',0,NULL,NULL,false,{Audit}),
                (17,3,6,5,'Each product page carries its own size chart, so you can check the exact measurements before you order.',0,NULL,NULL,false,{Audit}),
                (18,4,1,2,'Romp makes clothes for children who run, climb, spin and sit on the floor. We think clothes for the young should be comfortable first, beautiful second, and built to be handed down.',0,NULL,NULL,false,{Audit}),
                (19,4,2,1,'Quietly made',2,NULL,NULL,false,{Audit}),
                (20,4,3,2,'We keep our collections small and our colours calm. A few good pieces, chosen with care, beat a wardrobe of things that will not last the season.',0,NULL,NULL,false,{Audit}),
                (21,4,4,1,'For families',2,NULL,NULL,false,{Audit}),
                (22,4,5,2,'We build Romp around how families actually shop: clear sizes, honest descriptions and a person to talk to when you need one.',0,NULL,NULL,false,{Audit}),
                (23,5,1,2,'Our clothes are made in Pakistan, by people who know cloth. We work with our makers closely, and every piece is checked before it is ever offered to you.',0,NULL,NULL,false,{Audit}),
                (24,5,2,1,'Checked before it reaches you',2,NULL,NULL,false,{Audit}),
                (25,5,3,2,'Each garment passes a quality check, is labelled and counted into our warehouse, and is packed by hand when you order.',0,NULL,NULL,false,{Audit}),
                (26,5,4,1,'Fabric and finish',2,NULL,NULL,false,{Audit}),
                (27,5,5,2,'Fabric and care details are shown on every product page, so you know exactly what your child will be wearing and how to look after it.',0,NULL,NULL,false,{Audit}),
                (28,6,1,2,'We are glad to help with an order, a size or a question. The quickest way to reach us is by message.',0,NULL,NULL,false,{Audit}),
                (29,6,2,6,'',0,1,'WhatsApp',false,{Audit}),
                (30,6,3,6,'',0,2,'Email',false,{Audit}),
                (31,6,4,6,'',0,3,'Hours',false,{Audit});
                INSERT INTO "STFR"."PAGE_BLCK_ITM" ("ID","PAGE_BLCK_ID","ROW_NO","COLM_NO","TEXT","INSR_DTE","INSR_BY") VALUES
                (1,14,1,0,'Ask your child to stand relaxed, in light clothing.',{Audit}),
                (2,14,2,0,'Measure around the fullest part of the chest, keeping the tape level and snug, not tight.',{Audit}),
                (3,14,3,0,'Measure around the natural waist, just above the navel.',{Audit}),
                (4,14,4,0,'Measure height from the floor to the top of the head, standing against a wall.',{Audit});
                """);

            // The rows above carry explicit ids, so move each identity sequence past them.
            foreach (var table in new[]
            {
                "DRFT_MAIN", "NAV_ITEM", "AGE_STEP", "FOOT_COLM", "FOOT_LINK", "STNG_ITEM", "POPL_SRCH", "PAGE_MAIN", "PAGE_BLCK", "PAGE_BLCK_ITM",
            })
            {
                migrationBuilder.Sql(
                    $"SELECT setval(pg_get_serial_sequence('\"STFR\".\"{table}\"', 'ID'), (SELECT MAX(\"ID\") FROM \"STFR\".\"{table}\"));");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[]
            {
                "PAGE_BLCK_ITM", "PAGE_BLCK", "PAGE_MAIN", "POPL_SRCH", "STNG_ITEM", "FOOT_LINK", "FOOT_COLM", "AGE_STEP", "NAV_ITEM", "DRFT_MAIN",
            })
            {
                migrationBuilder.Sql($"DELETE FROM \"STFR\".\"{table}\";");
            }
        }
    }
}
