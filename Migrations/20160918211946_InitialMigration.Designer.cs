using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Hourai.Model;

namespace Hourai.Migrations
{
    [DbContext(typeof(BotDbContext))]
    [Migration("20160918211946_InitialMigration")]
    partial class InitialMigration
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.0.0-rtm-21431");

            modelBuilder.Entity("Hourai.Channel", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<ulong>("GuildId");

                    b.Property<bool>("BanMessage");

                    b.Property<bool>("JoinMessage");

                    b.Property<bool>("LeaveMessage");

                    b.Property<bool>("SearchIgnored");

                    b.HasKey("Id", "GuildId");

                    b.HasIndex("GuildId");

                    b.ToTable("channels");
                });

            modelBuilder.Entity("Hourai.CustomCommand", b =>
                {
                    b.Property<ulong>("GuildId");

                    b.Property<string>("Name");

                    b.Property<string>("Response")
                        .IsRequired();

                    b.HasKey("GuildId", "Name");

                    b.HasIndex("GuildId");

                    b.ToTable("commands");
                });

            modelBuilder.Entity("Hourai.Guild", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<bool>("IsBlacklisted");

                    b.Property<string>("MinRoles");

                    b.Property<long>("Modules");

                    b.HasKey("Id");

                    b.ToTable("guilds");
                });

            modelBuilder.Entity("Hourai.GuildUser", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<ulong>("GuildId");

                    b.Property<string>("BannedRoles");

                    b.Property<ulong?>("UserId");

                    b.HasKey("Id", "GuildId");

                    b.HasIndex("GuildId");

                    b.HasIndex("UserId");

                    b.ToTable("guild_users");
                });

            modelBuilder.Entity("Hourai.User", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<bool>("IsBlacklisted");

                    b.Property<string>("Username")
                        .IsRequired();

                    b.HasKey("Id");

                    b.ToTable("users");
                });

            modelBuilder.Entity("Hourai.Username", b =>
                {
                    b.Property<ulong>("UserId");

                    b.Property<DateTimeOffset>("Date");

                    b.Property<string>("Name")
                        .IsRequired();

                    b.HasKey("UserId", "Date");

                    b.HasIndex("UserId");

                    b.ToTable("usernames");
                });

            modelBuilder.Entity("Hourai.Channel", b =>
                {
                    b.HasOne("Hourai.Guild", "Guild")
                        .WithMany("Channels")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Hourai.CustomCommand", b =>
                {
                    b.HasOne("Hourai.Guild", "Guild")
                        .WithMany("Commands")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Hourai.GuildUser", b =>
                {
                    b.HasOne("Hourai.Guild", "Guild")
                        .WithMany("Users")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("Hourai.User")
                        .WithMany("GuildUsers")
                        .HasForeignKey("UserId");
                });

            modelBuilder.Entity("Hourai.Username", b =>
                {
                    b.HasOne("Hourai.User", "User")
                        .WithMany("Usernames")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
        }
    }
}
