using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hourai.Migrations
{
    public partial class RedditMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subreddits",
                columns: table => new
                {
                    Name = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subreddits", x => x.Name);
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

            migrationBuilder.CreateIndex(
                name: "IX_subreddit_channels_ChannelId_GuildId",
                table: "subreddit_channels",
                columns: new[] { "ChannelId", "GuildId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subreddit_channels");

            migrationBuilder.DropTable(
                name: "subreddits");
        }
    }
}
