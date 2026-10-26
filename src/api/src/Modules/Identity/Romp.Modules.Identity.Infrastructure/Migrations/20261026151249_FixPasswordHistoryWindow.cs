using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

namespace Romp.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixPasswordHistoryWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_USR_PWD_HIST_USR_ID",
                schema: "IDNT",
                table: "USR_PWD_HIST");

            migrationBuilder.AddForeignKey(
                name: "FK_USR_PWD_HIST_USR_ID",
                schema: "IDNT",
                table: "USR_PWD_HIST",
                column: "USR_ID",
                principalSchema: "IDNT",
                principalTable: "USR_MAIN",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_USR_PWD_HIST_USR_ID",
                schema: "IDNT",
                table: "USR_PWD_HIST");

            migrationBuilder.AddForeignKey(
                name: "FK_USR_PWD_HIST_USR_ID",
                schema: "IDNT",
                table: "USR_PWD_HIST",
                column: "USR_ID",
                principalSchema: "IDNT",
                principalTable: "USR_MAIN",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
