using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1861 // generated migration: constant index column arrays are fine

namespace Romp.Modules.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMfa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MFA_ENRL_DTE",
                schema: "IDNT",
                table: "USR_MAIN",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MFA_KEY_ID",
                schema: "IDNT",
                table: "USR_MAIN",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MFA_LAST_STEP",
                schema: "IDNT",
                table: "USR_MAIN",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<byte[]>(
                name: "MFA_SECR_ENC",
                schema: "IDNT",
                table: "USR_MAIN",
                type: "bytea",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MFA_ENRL_PEND",
                schema: "IDNT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    USR_ID = table.Column<long>(type: "bigint", nullable: false),
                    SECR_ENC = table.Column<byte[]>(type: "bytea", maxLength: 128, nullable: false),
                    KEY_ID = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EXPY_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MFA_ENRL_PEND", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MFA_ENRL_PEND_USR_ID",
                        column: x => x.USR_ID,
                        principalSchema: "IDNT",
                        principalTable: "USR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "USR_RCVR_CODE",
                schema: "IDNT",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    USR_ID = table.Column<long>(type: "bigint", nullable: false),
                    CODE_HASH = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    USED_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    INSR_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    INSR_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UPDT_DTE = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UPDT_BY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USR_RCVR_CODE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_USR_RCVR_CODE_USR_ID",
                        column: x => x.USR_ID,
                        principalSchema: "IDNT",
                        principalTable: "USR_MAIN",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MFA_ENRL_PEND_USR_ID",
                schema: "IDNT",
                table: "MFA_ENRL_PEND",
                column: "USR_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_USR_RCVR_CODE_USR_ID",
                schema: "IDNT",
                table: "USR_RCVR_CODE",
                column: "USR_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MFA_ENRL_PEND",
                schema: "IDNT");

            migrationBuilder.DropTable(
                name: "USR_RCVR_CODE",
                schema: "IDNT");

            migrationBuilder.DropColumn(
                name: "MFA_ENRL_DTE",
                schema: "IDNT",
                table: "USR_MAIN");

            migrationBuilder.DropColumn(
                name: "MFA_KEY_ID",
                schema: "IDNT",
                table: "USR_MAIN");

            migrationBuilder.DropColumn(
                name: "MFA_LAST_STEP",
                schema: "IDNT",
                table: "USR_MAIN");

            migrationBuilder.DropColumn(
                name: "MFA_SECR_ENC",
                schema: "IDNT",
                table: "USR_MAIN");
        }
    }
}
