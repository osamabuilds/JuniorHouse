using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Romp.Modules.Cart.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCartLineSeenPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SEEN_PRIC_AMT",
                schema: "CART",
                table: "CART_LINE",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SEEN_PRIC_AMT",
                schema: "CART",
                table: "CART_LINE");
        }
    }
}
