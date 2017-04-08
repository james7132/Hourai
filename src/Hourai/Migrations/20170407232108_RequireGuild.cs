using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hourai.Migrations
{
    public partial class RequireGuild : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_channels_guilds_GuildId",
                table: "channels");

            migrationBuilder.AlterColumn<ulong>(
                name: "GuildId",
                table: "channels",
                nullable: false,
                oldClrType: typeof(ulong),
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_channels_guilds_GuildId",
                table: "channels",
                column: "GuildId",
                principalTable: "guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_channels_guilds_GuildId",
                table: "channels");

            migrationBuilder.AlterColumn<ulong>(
                name: "GuildId",
                table: "channels",
                nullable: true,
                oldClrType: typeof(ulong));

            migrationBuilder.AddForeignKey(
                name: "FK_channels_guilds_GuildId",
                table: "channels",
                column: "GuildId",
                principalTable: "guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
