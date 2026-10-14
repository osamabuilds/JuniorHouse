using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Romp.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStylePriceChangedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TGT_PRIC_CHG_DTE",
                schema: "CTLG",
                table: "STYL_MAIN",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TGT_PRIC_CHG_DTE",
                schema: "CTLG",
                table: "STYL_MAIN");
        }
    }
}
