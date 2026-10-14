using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Romp.Modules.Labelling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameManualActionColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MNL_ACT_BY",
                schema: "SKU",
                table: "PRT_JOB",
                newName: "MNL_BY");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MNL_BY",
                schema: "SKU",
                table: "PRT_JOB",
                newName: "MNL_ACT_BY");
        }
    }
}
