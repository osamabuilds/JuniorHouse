using Microsoft.EntityFrameworkCore.Migrations;
using Romp.Modules.Vendor.Domain.Vendors;

#nullable disable

namespace Romp.Modules.Vendor.Infrastructure.Migrations
{
    /// <summary>SCRUM-93 task 41 (AC-39): a nullable note on each status-history row, used to record a Send confirmed without a tech pack. Existing rows stay null.</summary>
    public partial class AddPoStatusHistoryNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NOTE",
                schema: "VNDR",
                table: "PO_STS_HIST",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NOTE",
                schema: "VNDR",
                table: "PO_STS_HIST");
        }
    }
}
