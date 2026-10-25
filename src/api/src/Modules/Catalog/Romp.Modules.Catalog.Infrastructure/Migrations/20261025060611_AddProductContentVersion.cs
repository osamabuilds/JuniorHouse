using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Romp.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductContentVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CNTT_VER",
                schema: "CTLG",
                table: "PRDT_MAIN",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CNTT_VER",
                schema: "CTLG",
                table: "PRDT_MAIN");
        }
    }
}
