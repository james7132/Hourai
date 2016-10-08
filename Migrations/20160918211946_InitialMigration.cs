using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hourai.Migrations
{
    public partial class InitialMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guilds",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false),
                    IsBlacklisted = table.Column<bool>(nullable: false),
                    MinRoles = table.Column<string>(nullable: true),
                    Modules = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false),
                    IsBlacklisted = table.Column<bool>(nullable: false),
                    Username = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    BanMessage = table.Column<bool>(nullable: false),
                    JoinMessage = table.Column<bool>(nullable: false),
                    LeaveMessage = table.Column<bool>(nullable: false),
                    SearchIgnored = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channels", x => new { x.Id, x.GuildId });
                    table.ForeignKey(
                        name: "FK_channels_guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "commands",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Response = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commands", x => new { x.GuildId, x.Name });
                    table.ForeignKey(
                        name: "FK_commands_guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guild_users",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    BannedRoles = table.Column<string>(nullable: true),
                    UserId = table.Column<ulong>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guild_users", x => new { x.Id, x.GuildId });
                    table.ForeignKey(
                        name: "FK_guild_users_guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_guild_users_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usernames",
                columns: table => new
                {
                    UserId = table.Column<ulong>(nullable: false),
                    Date = table.Column<DateTimeOffset>(nullable: false),
                    Name = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usernames", x => new { x.UserId, x.Date });
                    table.ForeignKey(
                        name: "FK_usernames_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_channels_GuildId",
                table: "channels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_commands_GuildId",
                table: "commands",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_guild_users_GuildId",
                table: "guild_users",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_guild_users_UserId",
                table: "guild_users",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_usernames_UserId",
                table: "usernames",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropTable(
                name: "commands");

            migrationBuilder.DropTable(
                name: "guild_users");

            migrationBuilder.DropTable(
                name: "usernames");

            migrationBuilder.DropTable(
                name: "guilds");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
