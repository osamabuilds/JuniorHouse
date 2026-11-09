using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Romp.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "USR_MAIN",
                schema: "IDNT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    USR_NAME = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DISP_NAME = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PWD_HASH = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ACT_IND = table.Column<bool>(type: "boolean", nullable: false),
                    MUST_CHNG_PWD_IND = table.Column<bool>(type: "boolean", nullable: false),
                    TEMP_PWD_EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TEMP_PWD_USED_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USR_MAIN", x => x.ID);
                    table.CheckConstraint("CK_USR_MAIN_USR_NAME", "\"USR_NAME\" ~ '^[a-z0-9._-]{3,40}$'");
                });

            migrationBuilder.CreateTable(
                name: "USR_PWD_HIST",
                schema: "IDNT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    USR_ID = table.Column<long>(type: "bigint", nullable: false),
                    PWD_HASH = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RETD_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USR_PWD_HIST", x => x.ID);
                    table.ForeignKey(
                        name: "FK_USR_PWD_HIST_USR_ID",
                        column: x => x.USR_ID,
                        principalSchema: "IDNT",
                        principalTable: "USR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "USR_ROLE_MAP",
                schema: "IDNT",
                columns: table => new
                {
                    USR_ID = table.Column<long>(type: "bigint", nullable: false),
                    ROLE_ID = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USR_ROLE_MAP", x => new { x.USR_ID, x.ROLE_ID });
                    table.ForeignKey(
                        name: "FK_USR_ROLE_MAP_ROLE_ID",
                        column: x => x.ROLE_ID,
                        principalSchema: "IDNT",
                        principalTable: "ROLE_LKP",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_USR_ROLE_MAP_USR_ID",
                        column: x => x.USR_ID,
                        principalSchema: "IDNT",
                        principalTable: "USR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_USR_MAIN_USR_NAME",
                schema: "IDNT",
                table: "USR_MAIN",
                column: "USR_NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USR_PWD_HIST_USR_ID",
                schema: "IDNT",
                table: "USR_PWD_HIST",
                column: "USR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_USR_ROLE_MAP_ROLE_ID",
                schema: "IDNT",
                table: "USR_ROLE_MAP",
                column: "ROLE_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "USR_PWD_HIST",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "USR_ROLE_MAP",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "USR_MAIN",
                schema: "IDNT");
        }
    }
}
