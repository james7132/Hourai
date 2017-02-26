using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Hourai.Model;

namespace Hourai.Migrations
{
    [DbContext(typeof(BotDbContext))]
    [Migration("20170226015017_Test")]
    partial class Test
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.0-rtm-22752");

            modelBuilder.Entity("Hourai.Model.AbstractTempAction", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Discriminator")
                        .IsRequired();

                    b.Property<DateTimeOffset>("Expiration");

                    b.Property<ulong>("GuildId");

                    b.Property<ulong>("UserId");

                    b.HasKey("Id");

                    b.HasIndex("Expiration")
                        .HasName("IX_temp_actions_Expiration");

                    b.ToTable("temp_actions");

                    b.HasDiscriminator<string>("Discriminator").HasValue("AbstractTempAction");
                });

            modelBuilder.Entity("Hourai.Model.Channel", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<ulong>("GuildId");

                    b.Property<bool>("BanMessage");

                    b.Property<bool>("JoinMessage");

                    b.Property<bool>("LeaveMessage");

                    b.Property<bool>("StreamMessage");

                    b.Property<bool>("VoiceMessage");

                    b.HasKey("Id", "GuildId");

                    b.HasIndex("GuildId");

                    b.ToTable("channels");
                });

            modelBuilder.Entity("Hourai.Model.CustomCommand", b =>
                {
                    b.Property<ulong>("GuildId");

                    b.Property<string>("Name");

                    b.Property<string>("Response")
                        .IsRequired();

                    b.HasKey("GuildId", "Name");

                    b.HasIndex("GuildId");

                    b.ToTable("commands");
                });

            modelBuilder.Entity("Hourai.Model.Guild", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<bool>("IsBlacklisted");

                    b.Property<string>("MinRoles");

                    b.Property<string>("Prefix")
                        .IsRequired()
                        .HasDefaultValue("~")
                        .HasMaxLength(1);

                    b.HasKey("Id");

                    b.ToTable("guilds");
                });

            modelBuilder.Entity("Hourai.Model.GuildUser", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<ulong>("GuildId");

                    b.HasKey("Id", "GuildId");

                    b.HasIndex("GuildId");

                    b.ToTable("guild_users");
                });

            modelBuilder.Entity("Hourai.Model.Role", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<ulong>("GuildId");

                    b.HasKey("Id", "GuildId");

                    b.HasIndex("GuildId");

                    b.ToTable("roles");
                });

            modelBuilder.Entity("Hourai.Model.Subreddit", b =>
                {
                    b.Property<string>("Name");

                    b.Property<DateTimeOffset?>("LastPost");

                    b.HasKey("Name");

                    b.ToTable("subreddits");
                });

            modelBuilder.Entity("Hourai.Model.SubredditChannel", b =>
                {
                    b.Property<string>("Name");

                    b.Property<ulong>("ChannelId");

                    b.Property<ulong>("GuildId");

                    b.HasKey("Name", "ChannelId");

                    b.HasIndex("GuildId");

                    b.HasIndex("ChannelId", "GuildId");

                    b.ToTable("subreddit_channels");
                });

            modelBuilder.Entity("Hourai.Model.User", b =>
                {
                    b.Property<ulong>("Id");

                    b.Property<bool>("IsBlacklisted");

                    b.Property<string>("Username")
                        .IsRequired();

                    b.HasKey("Id");

                    b.ToTable("users");
                });

            modelBuilder.Entity("Hourai.Model.Username", b =>
                {
                    b.Property<ulong>("UserId");

                    b.Property<DateTimeOffset>("Date");

                    b.Property<string>("Name")
                        .IsRequired();

                    b.HasKey("UserId", "Date");

                    b.HasIndex("UserId");

                    b.ToTable("usernames");
                });

            modelBuilder.Entity("Hourai.Model.UserRole", b =>
                {
                    b.Property<ulong>("UserId");

                    b.Property<ulong>("GuildId");

                    b.Property<ulong>("RoleId");

                    b.Property<bool>("HasRole");

                    b.Property<bool>("IsBanned");

                    b.HasKey("UserId", "GuildId", "RoleId");

                    b.HasIndex("RoleId", "GuildId");

                    b.ToTable("user_role");
                });

            modelBuilder.Entity("Hourai.Model.TempBan", b =>
                {
                    b.HasBaseType("Hourai.Model.AbstractTempAction");


                    b.ToTable("temp_actions");

                    b.HasDiscriminator().HasValue("TempBan");
                });

            modelBuilder.Entity("Hourai.Model.TempRole", b =>
                {
                    b.HasBaseType("Hourai.Model.AbstractTempAction");

                    b.Property<ulong?>("RoleGuildId");

                    b.Property<ulong>("RoleId");

                    b.Property<ulong?>("RoleId1");

                    b.HasIndex("RoleId1", "RoleGuildId");

                    b.ToTable("temp_actions");

                    b.HasDiscriminator().HasValue("TempRole");
                });

            modelBuilder.Entity("Hourai.Model.Channel", b =>
                {
                    b.HasOne("Hourai.Model.Guild", "Guild")
                        .WithMany("Channels")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Hourai.Model.CustomCommand", b =>
                {
                    b.HasOne("Hourai.Model.Guild", "Guild")
                        .WithMany("Commands")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Hourai.Model.GuildUser", b =>
                {
                    b.HasOne("Hourai.Model.Guild", "Guild")
                        .WithMany("Users")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("Hourai.Model.User", "User")
                        .WithMany("GuildUsers")
                        .HasForeignKey("Id")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Hourai.Model.Role", b =>
                {
                    b.HasOne("Hourai.Model.Guild", "Guild")
                        .WithMany("Roles")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Hourai.Model.SubredditChannel", b =>
                {
                    b.HasOne("Hourai.Model.Guild", "Guild")
                        .WithMany()
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("Hourai.Model.Subreddit", "Subreddit")
                        .WithMany("Channels")
                        .HasForeignKey("Name")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("Hourai.Model.Channel", "Channel")
                        .WithMany("Subreddits")
                        .HasForeignKey("ChannelId", "GuildId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Hourai.Model.Username", b =>
                {
                    b.HasOne("Hourai.Model.User", "User")
                        .WithMany("Usernames")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Hourai.Model.UserRole", b =>
                {
                    b.HasOne("Hourai.Model.Role", "Role")
                        .WithMany("Users")
                        .HasForeignKey("RoleId", "GuildId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("Hourai.Model.GuildUser", "User")
                        .WithMany("Roles")
                        .HasForeignKey("UserId", "GuildId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Hourai.Model.TempRole", b =>
                {
                    b.HasOne("Hourai.Model.Role", "Role")
                        .WithMany()
                        .HasForeignKey("RoleId1", "RoleGuildId");
                });
        }
    }
}
