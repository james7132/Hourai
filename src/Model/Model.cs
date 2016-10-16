using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public class BotDbContext : DbContext {

  // Discord Data
  public DbSet<Guild> Guilds { get; set; }
  public DbSet<Channel> Channels { get; set; }
  public DbSet<User> Users { get; set; }
  public DbSet<Username> Usernames { get; set; }
  public DbSet<GuildUser> GuildUsers { get; set; }
  public DbSet<CustomCommand> Commands { get; set; }

  // Temporary Action Data
  public DbSet<AbstractTempAction> TempActions { get; set; }
  public DbSet<TempBan> TempBans { get; set; }
  public DbSet<TempRole> TempRole { get; set; }

  public bool AllowSave { get; set; } = true;

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
    Config.Load();
    optionsBuilder.UseSqlite($"Filename={Config.DbFilename}");
  }

  protected override void OnModelCreating(ModelBuilder builder) {
    builder.Entity<Channel>()
      .HasKey(c => new { c.Id, c.GuildId });
    builder.Entity<GuildUser>()
      .HasKey(u => new { u.Id, u.GuildId });
    builder.Entity<CustomCommand>()
      .HasKey(c => new { c.GuildId, c.Name });
    builder.Entity<Username>()
      .HasKey(c => new { c.UserId, c.Date });
    builder.Entity<AbstractTempAction>()
      .HasIndex(t => t.End)
        .HasName("IX_temp_actions_End");
  }

  public async Task Save() {
    if(!AllowSave)
      return;
    var changes = await SaveChangesAsync();
    if(changes > 0)
      Log.Info($"Saved {changes} changes to the database.");
  }

  public Guild FindGuild(IGuild iguild) {
    Check.NotNull(iguild);
    return Guilds.Include(g => g.Commands) 
      .Include(g => g.Channels)
      .FirstOrDefault(g => g.Id == iguild.Id); 
  }

  public async Task<Guild> GetGuild(IGuild iguild) {
    var guild = FindGuild(iguild);
    if(guild == null) { 
      guild = new Guild(iguild); 
      Guilds.Add(guild);
      await Save();
    }
    return guild;
  }

  public async Task<bool> RemoveGuild(IGuild iguild) {
    var guild = FindGuild(iguild);
    if(guild == null)
      return false;
    Guilds.Remove(guild);
    await Save();
    return true;
  }

  public User FindUser(IUser iuser) {
    Check.NotNull(iuser);
    return Users.Include(u => u.Usernames)
      .FirstOrDefault(u => u.Id == iuser.Id);
  }

  public async Task<User> GetUser(IUser iuser) {
    var user = FindUser(iuser);
    if(user == null) {
      user = new User(iuser);
      Users.Add(user);
      await Save();
    }
    return user;
  }

  public async Task<bool> RemoveUser(IUser iuser) {
    var user = FindUser(iuser);
    if(user == null)
      return false;
    Users.Remove(user);
    await Save();
    return true;
  }

  public GuildUser FindGuildUser(IGuildUser iuser) {
    Check.NotNull(iuser);
    return GuildUsers.FirstOrDefault(u => 
        (u.Id == iuser.Id) && (u.GuildId == iuser.Guild.Id));
  }

  public async Task<GuildUser> GetGuildUser(IGuildUser iuser) {
    var user = FindGuildUser(iuser);
    if(user == null) {
      user = new GuildUser(iuser);
      user.User = FindUser(iuser);
      if(user.User == null) {
        user.User = new User(iuser);
        Users.Add(user.User);
      }
      GuildUsers.Add(user);
      await Save();
    }
    return user;
  }

  public async Task<bool> RemoveGuildUser(IGuildUser iuser) {
    var user = FindGuildUser(iuser);
    if(user == null)
      return false;
    GuildUsers.Remove(user);
    await Save();
    return true;
  }

  public Channel FindChannel(IGuildChannel ichannel) {
    Check.NotNull(ichannel);
    return Channels.FirstOrDefault(c => 
        (c.Id == ichannel.Id) && (c.GuildId == ichannel.Guild.Id));
  }

  public async Task<Channel> GetChannel(IGuildChannel ichannel) {
    var channel = FindChannel(ichannel);
    if(channel == null) {
      channel = new Channel(ichannel);
      Channels.Add(channel);
      await Save();
    }
    return channel;
  }

  public async Task<bool> RemoveChannel(IGuildChannel ichannel) {
    var channel = FindChannel(ichannel);
    if(channel == null)
      return false;
    Channels.Remove(channel);
    await Save();
    return true;
  }

}

[Table("commands")]
public class CustomCommand {

  [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong GuildId { get; set; }
  public Guild Guild { get; set; }
  [Required]
  public string Name { get; set; }
  [Required]
  public string Response { get; set; }

  public CustomCommand() {
  }

  public Task Execute(IMessage message, string input) {
    var channel = Check.NotNull(message.Channel as ITextChannel);
    return message.Respond(Response.Replace("$input", input)
      .Replace("$user", message.Author.Mention)
      .Replace("$channel", channel.Mention));
  }

}

}
