using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hourai.Migrations
{
    public partial class Test : Migration
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
                    Prefix = table.Column<string>(maxLength: 1, nullable: false, defaultValue: "~")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "subreddits",
                columns: table => new
                {
                    Name = table.Column<string>(nullable: false),
                    LastPost = table.Column<DateTimeOffset>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subreddits", x => x.Name);
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
                    StreamMessage = table.Column<bool>(nullable: false),
                    VoiceMessage = table.Column<bool>(nullable: false)
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
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => new { x.Id, x.GuildId });
                    table.ForeignKey(
                        name: "FK_roles_guilds_GuildId",
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
                    GuildId = table.Column<ulong>(nullable: false)
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
                        name: "FK_guild_users_users_Id",
                        column: x => x.Id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "subreddit_channels",
                columns: table => new
                {
                    Name = table.Column<string>(nullable: false),
                    ChannelId = table.Column<ulong>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subreddit_channels", x => new { x.Name, x.ChannelId });
                    table.ForeignKey(
                        name: "FK_subreddit_channels_guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subreddit_channels_subreddits_Name",
                        column: x => x.Name,
                        principalTable: "subreddits",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subreddit_channels_channels_ChannelId_GuildId",
                        columns: x => new { x.ChannelId, x.GuildId },
                        principalTable: "channels",
                        principalColumns: new[] { "Id", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "temp_actions",
                columns: table => new
                {
                    Id = table.Column<ulong>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Discriminator = table.Column<string>(nullable: false),
                    Expiration = table.Column<DateTimeOffset>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    UserId = table.Column<ulong>(nullable: false),
                    RoleGuildId = table.Column<ulong>(nullable: true),
                    RoleId = table.Column<ulong>(nullable: true),
                    RoleId1 = table.Column<ulong>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_temp_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_temp_actions_roles_RoleId1_RoleGuildId",
                        columns: x => new { x.RoleId1, x.RoleGuildId },
                        principalTable: "roles",
                        principalColumns: new[] { "Id", "GuildId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_role",
                columns: table => new
                {
                    UserId = table.Column<ulong>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    RoleId = table.Column<ulong>(nullable: false),
                    HasRole = table.Column<bool>(nullable: false),
                    IsBanned = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role", x => new { x.UserId, x.GuildId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_role_roles_RoleId_GuildId",
                        columns: x => new { x.RoleId, x.GuildId },
                        principalTable: "roles",
                        principalColumns: new[] { "Id", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_role_guild_users_UserId_GuildId",
                        columns: x => new { x.UserId, x.GuildId },
                        principalTable: "guild_users",
                        principalColumns: new[] { "Id", "GuildId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_temp_actions_Expiration",
                table: "temp_actions",
                column: "Expiration");

            migrationBuilder.CreateIndex(
                name: "IX_temp_actions_RoleId1_RoleGuildId",
                table: "temp_actions",
                columns: new[] { "RoleId1", "RoleGuildId" });

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
                name: "IX_roles_GuildId",
                table: "roles",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_subreddit_channels_GuildId",
                table: "subreddit_channels",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_subreddit_channels_ChannelId_GuildId",
                table: "subreddit_channels",
                columns: new[] { "ChannelId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_usernames_UserId",
                table: "usernames",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_role_RoleId_GuildId",
                table: "user_role",
                columns: new[] { "RoleId", "GuildId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "temp_actions");

            migrationBuilder.DropTable(
                name: "commands");

            migrationBuilder.DropTable(
                name: "subreddit_channels");

            migrationBuilder.DropTable(
                name: "usernames");

            migrationBuilder.DropTable(
                name: "user_role");

            migrationBuilder.DropTable(
                name: "subreddits");

            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "guild_users");

            migrationBuilder.DropTable(
                name: "guilds");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
