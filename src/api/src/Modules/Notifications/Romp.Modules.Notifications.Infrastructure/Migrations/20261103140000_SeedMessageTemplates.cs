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
    /// Seeds version 1 of the message templates (CHN-9). The wording is placeholder copy until the business supplies it (Q-OCF-1); the business
    /// changes it by adding a higher version row, never by editing code. Placeholders: OrderNumber, CustomerName, Total, Lines.
    /// </summary>
    [DbContext(typeof(NtfyDbContext))]
    [Migration("20261103140000_SeedMessageTemplates")]
    public partial class SeedMessageTemplates : Migration
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
            var at = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

            // Kind ids: 1 order confirmation, 2 order cancelled, 3 COD confirmation request. Channel ids: 1 email, 2 WhatsApp.
            migrationBuilder.InsertData(
                schema: "NTFY",
                table: "NTFY_TMPL",
                columns: Columns,
                columnTypes: ColumnTypes,
                values: new object[,]
                {
                    { (short)1, (short)1, "EN", (short)1, "Your Romp order {{OrderNumber}}",
                      "Hi {{CustomerName}},\n\nThank you for your order.\n\nOrder {{OrderNumber}}\n{{Lines}}\nTotal: {{Total}}",
                      "<p>Hi {{CustomerName}},</p><p>Thank you for your order.</p><p>Order {{OrderNumber}}</p>{{Lines}}<p>Total: {{Total}}</p>",
                      null, true, at, "seed" },
                    { (short)1, (short)2, "EN", (short)1, "Order confirmed",
                      "Hi {{CustomerName}}, thank you for your order {{OrderNumber}}.\n{{Lines}}\nTotal: {{Total}}",
                      null, "romp_order_confirmation", true, at, "seed" },
                    { (short)2, (short)1, "EN", (short)1, "Your Romp order {{OrderNumber}} was cancelled",
                      "Hi {{CustomerName}},\n\nYour order {{OrderNumber}} has been cancelled.",
                      "<p>Hi {{CustomerName}},</p><p>Your order {{OrderNumber}} has been cancelled.</p>",
                      null, true, at, "seed" },
                    { (short)2, (short)2, "EN", (short)1, "Order cancelled",
                      "Hi {{CustomerName}}, your order {{OrderNumber}} has been cancelled.",
                      null, "romp_order_cancelled", true, at, "seed" },
                    { (short)3, (short)1, "EN", (short)1, "Please confirm your Romp order {{OrderNumber}}",
                      "Hi {{CustomerName}},\n\nPlease confirm your cash on delivery order {{OrderNumber}}.\n{{Lines}}\nTotal: {{Total}}",
                      "<p>Hi {{CustomerName}},</p><p>Please confirm your cash on delivery order {{OrderNumber}}.</p>{{Lines}}<p>Total: {{Total}}</p>",
                      null, true, at, "seed" },
                    { (short)3, (short)2, "EN", (short)1, "Confirm your order",
                      "Hi {{CustomerName}}, please confirm your cash on delivery order {{OrderNumber}} (Total: {{Total}}).",
                      null, "romp_cod_confirmation", true, at, "seed" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DELETE FROM "NTFY"."NTFY_TMPL" WHERE "VER" = 1 AND "INSR_BY" = 'seed'""");
        }
    }
}
