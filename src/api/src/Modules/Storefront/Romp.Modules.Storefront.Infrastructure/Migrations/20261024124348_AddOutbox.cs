using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Romp.Modules.Storefront.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OUTB_MSG",
                schema: "STFR",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EVNT_TYP = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PYLD = table.Column<string>(type: "jsonb", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AGGR_TYP = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PurchaseOrder"),
                    AGGR_ID = table.Column<long>(type: "bigint", nullable: true),
                    MSG_VER = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    ATMP_CNT = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    NXT_ATMP_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CLM_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LEAS_EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PROC_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DEDL_IND = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DEDL_RSN = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OUTB_MSG", x => x.ID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OUTB_MSG",
                schema: "STFR");
        }
    }
}
