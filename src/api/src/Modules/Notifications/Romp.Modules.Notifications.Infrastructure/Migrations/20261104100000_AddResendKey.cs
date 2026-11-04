using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Romp.Modules.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResendKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RSND_KEY",
                schema: "NTFY",
                table: "NTFY_CHNL",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_NTFY_CHNL_RSND_KEY",
                schema: "NTFY",
                table: "NTFY_CHNL",
                column: "RSND_KEY",
                unique: true,
                filter: "\"RSND_KEY\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_NTFY_CHNL_RSND_KEY",
                schema: "NTFY",
                table: "NTFY_CHNL");

            migrationBuilder.DropColumn(
                name: "RSND_KEY",
                schema: "NTFY",
                table: "NTFY_CHNL");
        }
    }
}
