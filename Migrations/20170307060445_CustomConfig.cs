using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hourai.Migrations
{
    public partial class CustomConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "custom_config",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(nullable: false),
                    ConfigString = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_config", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_custom_config_guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_config");
        }
    }
}
