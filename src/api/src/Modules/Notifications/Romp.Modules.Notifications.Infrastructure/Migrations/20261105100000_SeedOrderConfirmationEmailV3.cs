using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Romp.Modules.Notifications.Infrastructure.Persistence;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // migration: constant arrays are fine

namespace Romp.Modules.Notifications.Infrastructure.Migrations
{
    /// <summary>
    /// Version 3 of the order-confirmation email: adds a "Track your order" link ({{TrackLink}}, Q-OLK-4). The cancellation and COD request
    /// emails stay at version 2. The WhatsApp templates are approved by the provider with fixed placeholders, so they stay at version 1.
    /// </summary>
    [DbContext(typeof(NtfyDbContext))]
    [Migration("20261105100000_SeedOrderConfirmationEmailV3")]
    public partial class SeedOrderConfirmationEmailV3 : Migration
    {
        private static readonly string[] Columns =
        [
            "NTFY_KIND_ID", "CHNL_ID", "LANG", "VER", "SUBJ", "TEXT_BODY", "HTML_BODY", "PRVD_TMPL_NAME", "ACT_IND", "INSR_DTE", "INSR_BY",
        ];

        private static readonly string[] ColumnTypes =
        [
            "smallint", "smallint", "character varying(5)", "smallint", "character varying(200)", "character varying(4000)",
            "character varying(20000)", "character varying(100)", "boolean", "timestamp with time zone", "character varying(100)",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var at = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "NTFY_TMPL",
                columns: Columns,
                columnTypes: ColumnTypes,
                values: new object[,]
                {
                    { (short)1, (short)1, "EN", (short)3, "Your Romp order {{OrderNumber}}",
                      "Hi {{CustomerName}},\n\nThank you for your order.\n\nOrder {{OrderNumber}}\n{{Lines}}\nTotal: {{Total}}\n\n{{NextStep}}\n\nTrack your order: {{TrackLink}}",
                      "<p>Hi {{CustomerName}},</p><p>Thank you for your order.</p><p>Order {{OrderNumber}}</p>{{Lines}}<p>Total: {{Total}}</p><p>{{NextStep}}</p><p><a href=\"{{TrackLink}}\">Track your order</a></p>",
                      null, true, at, "seed" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "NTFY"."NTFY_TMPL" WHERE "NTFY_KIND_ID" = 1 AND "CHNL_ID" = 1 AND "VER" = 3 AND "INSR_BY" = 'seed'""");
        }
    }
}
