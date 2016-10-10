using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hourai.Migrations
{
    public partial class TempActionMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "temp_bans");

            migrationBuilder.CreateTable(
                name: "temp_actions",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Autoincrement", true),
                    Discriminator = table.Column<string>(nullable: false),
                    End = table.Column<DateTimeOffset>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    Start = table.Column<DateTimeOffset>(nullable: false),
                    UserId = table.Column<ulong>(nullable: false),
                    RoleId = table.Column<ulong>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temp_actions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_temp_actions_End",
                table: "temp_actions",
                column: "End");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "temp_actions");

            migrationBuilder.CreateTable(
                name: "temp_bans",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false),
                    End = table.Column<DateTimeOffset>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    Start = table.Column<DateTimeOffset>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temp_bans", x => x.Id);
                });
        }
    }
}
