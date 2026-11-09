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
    /// Version 2 of the two order emails: the confirmation says what happens next ({{NextStep}}, OCF-2) and the cancellation names the reason
    /// category ({{Reason}}, OCF-4). WhatsApp templates are approved by the provider with fixed placeholders, so they stay at version 1.
    /// </summary>
    [DbContext(typeof(NtfyDbContext))]
    [Migration("20261104090000_SeedOrderMessageTemplatesV2")]
    public partial class SeedOrderMessageTemplatesV2 : Migration
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
            var at = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero);

            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "NTFY_TMPL",
                columns: Columns,
                columnTypes: ColumnTypes,
                values: new object[,]
                {
                    { (short)1, (short)1, "EN", (short)2, "Your Romp order {{OrderNumber}}",
                      "Hi {{CustomerName}},\n\nThank you for your order.\n\nOrder {{OrderNumber}}\n{{Lines}}\nTotal: {{Total}}\n\n{{NextStep}}",
                      "<p>Hi {{CustomerName}},</p><p>Thank you for your order.</p><p>Order {{OrderNumber}}</p>{{Lines}}<p>Total: {{Total}}</p><p>{{NextStep}}</p>",
                      null, true, at, "seed" },
                    { (short)2, (short)1, "EN", (short)2, "Your Romp order {{OrderNumber}} was cancelled",
                      "Hi {{CustomerName}},\n\nYour order {{OrderNumber}} has been cancelled because {{Reason}}.",
                      "<p>Hi {{CustomerName}},</p><p>Your order {{OrderNumber}} has been cancelled because {{Reason}}.</p>",
                      null, true, at, "seed" },
                    { (short)3, (short)1, "EN", (short)2, "Please confirm your Romp order {{OrderNumber}}",
                      "Hi {{CustomerName}},\n\nPlease confirm your cash on delivery order {{OrderNumber}}.\n{{Lines}}\nTotal: {{Total}}\n\nConfirm my order: {{ConfirmLink}}",
                      "<p>Hi {{CustomerName}},</p><p>Please confirm your cash on delivery order {{OrderNumber}}.</p>{{Lines}}<p>Total: {{Total}}</p><p><a href=\"{{ConfirmLink}}\">Confirm my order</a></p>",
                      null, true, at, "seed" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "NTFY"."NTFY_TMPL" WHERE "VER" = 2 AND "INSR_BY" = 'seed'""");
        }
    }
}
