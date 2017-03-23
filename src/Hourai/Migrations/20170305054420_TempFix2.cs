using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hourai.Migrations
{
    public partial class TempFix2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_temp_actions_UserId_GuildId",
                table: "temp_actions",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.AddForeignKey(
                name: "FK_temp_actions_guild_users_UserId_GuildId",
                table: "temp_actions",
                columns: new[] { "UserId", "GuildId" },
                principalTable: "guild_users",
                principalColumns: new[] { "Id", "GuildId" },
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_temp_actions_guild_users_UserId_GuildId",
                table: "temp_actions");

            migrationBuilder.DropIndex(
                name: "IX_temp_actions_UserId_GuildId",
                table: "temp_actions");
        }
    }
}
