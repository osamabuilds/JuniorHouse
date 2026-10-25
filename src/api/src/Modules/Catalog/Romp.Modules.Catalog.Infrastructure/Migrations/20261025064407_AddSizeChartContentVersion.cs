using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Romp.Modules.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSizeChartContentVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "CHRT_VER_SEQ",
                schema: "CTLG");

            migrationBuilder.AddColumn<long>(
                name: "CNTT_VER",
                schema: "CTLG",
                table: "CHRT_MAIN",
                type: "bigint",
                nullable: false,
                defaultValueSql: "nextval('\"CTLG\".\"CHRT_VER_SEQ\"')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CNTT_VER",
                schema: "CTLG",
                table: "CHRT_MAIN");

            migrationBuilder.DropSequence(
                name: "CHRT_VER_SEQ",
                schema: "CTLG");
        }
    }
}
