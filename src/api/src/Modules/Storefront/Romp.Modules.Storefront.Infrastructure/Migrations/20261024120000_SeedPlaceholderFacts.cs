using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Romp.Modules.Storefront.Infrastructure.Persistence;

#nullable disable

namespace Romp.Modules.Storefront.Infrastructure.Migrations
{
    /// <summary>
    /// Placeholder business facts so the seeded pages have something to quote while the owner reviews them (owner instruction,
    /// 2026-10-24: "for others set any data for now"). Only the contact email is real. Opening hours, delivery cities, times and charges
    /// and the exchange policy are PLACEHOLDERS to be replaced in the admin app before launch.
    /// </summary>
    [DbContext(typeof(StfrDbContext))]
    [Migration("20261024120000_SeedPlaceholderFacts")]
    public partial class SeedPlaceholderFacts : Migration
    {
        private const string Audit = "now(), 'migration'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Setting keys: 4 email, 5 hours.
            migrationBuilder.Sql($"""
                INSERT INTO "STFR"."STNG_ITEM" ("ID","STNG_KEY_ID","VAL","INSR_DTE","INSR_BY") VALUES
                (6,4,'osamabuss048@gmail.com',{Audit}),
                (7,5,'Monday to Saturday, 10am to 7pm',{Audit});
                INSERT INTO "STFR"."DLVR_RULE" ("ID","CITY","MIN_DAY","MAX_DAY","CHRG_AMT","FREE_THRS_AMT","CRER","ACT_IND","INSR_DTE","INSR_BY") VALUES
                (1,'Lahore',2,3,250.00,5000.00,'TCS',true,{Audit}),
                (2,'Karachi',3,4,300.00,5000.00,'TCS',true,{Audit}),
                (3,'Islamabad',3,4,300.00,5000.00,'TCS',true,{Audit}),
                (4,'Rest of Pakistan',4,6,350.00,5000.00,'TCS',true,{Audit});
                INSERT INTO "STFR"."XCHG_POLY" ("ID","WNDW_DAY","INSR_DTE","INSR_BY") VALUES (1,7,{Audit});
                INSERT INTO "STFR"."XCHG_COND" ("ID","XCHG_POLY_ID","TEXT","SORT_SEQ","INSR_DTE","INSR_BY") VALUES
                (1,1,'Items must be unworn and unwashed, with the original tags attached.',1,{Audit}),
                (2,1,'Exchanges are for a different size or colour, subject to stock.',2,{Audit}),
                (3,1,'Items bought on sale are exchanged for the same item only.',3,{Audit});
                """);

            foreach (var table in new[] { "STNG_ITEM", "DLVR_RULE", "XCHG_POLY", "XCHG_COND" })
            {
                migrationBuilder.Sql(
                    $"SELECT setval(pg_get_serial_sequence('\"STFR\".\"{table}\"', 'ID'), (SELECT MAX(\"ID\") FROM \"STFR\".\"{table}\"));");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "STFR"."XCHG_COND";
                DELETE FROM "STFR"."XCHG_POLY";
                DELETE FROM "STFR"."DLVR_RULE";
                DELETE FROM "STFR"."STNG_ITEM" WHERE "STNG_KEY_ID" IN (4,5);
                """);
        }
    }
}
