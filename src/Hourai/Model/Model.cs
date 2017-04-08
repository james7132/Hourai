using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Hourai.Model {

public static class DbSetExtensions {

  public static async Task<T> Get<T>(this DbSet<T> set, IEntity<ulong> entity) where T : class {
    var val = await set.FindAsync(entity.Id);
    if(val == null) {
      val = (T) Activator.CreateInstance(typeof(T), entity);
      set.Add(val);
    }
    return val;
  }

  public static async Task<bool> Remove<T>(this DbSet<T> set, IEntity<ulong> entity) where T : class {
    var val = await set.FindAsync(entity.Id);
    if(val == null)
      return false;
    set.Remove(val);
    return true;
  }

  public static async Task<Channel> Get(this DbSet<Channel> set, ITextChannel ichannel) {
    var channel = await set.FindAsync(ichannel.Id);
    if (channel == null)
      channel = set.Add(new Channel(ichannel) {
            GuildId = channel.Guild.Id
          }).Entity;
    return channel;
  }

}

public class BotDbContext : DbContext {

  // Discord Data
  public DbSet<Guild> Guilds { get; set; }
  public DbSet<Channel> Channels { get; set; }
  public DbSet<User> Users { get; set; }
  public DbSet<Username> Usernames { get; set; }
  public DbSet<GuildUser> GuildUsers { get; set; }
  public DbSet<CustomCommand> Commands { get; set; }
  public DbSet<Role> Roles { get; set; }
  public DbSet<UserRole> UserRoles { get; set; }
  public DbSet<MinRole> MinRoles { get; set; }

  public DbSet<CustomConfig> Configs { get; set; }

  // Temporary Action Data
  public DbSet<AbstractTempAction> TempActions { get; set; }
  public DbSet<TempBan> TempBans { get; set; }
  public DbSet<TempRole> TempRoles { get; set; }
  public DbSet<TempMute> TempMutes { get; set; }
  public DbSet<TempDeafen> TempDeafens { get; set; }

  //// Analytics Data
  //public DbSet<Counter> Counters { get; set; }

  // Service Data
  public DbSet<Subreddit> Subreddits { get; set; }
  public DbSet<SubredditChannel> SubredditChannels { get; set; }

  public bool AllowSave { get; set; } = true;

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
    if (!Config.IsLoaded) {
      Config.Load();
      Console.WriteLine($"Database File: {Config.DbFilename}");
    }
    optionsBuilder.UseMySql(Config.DbFilename);
  }

  protected override void OnModelCreating(ModelBuilder builder) {
    builder.Entity<CustomCommand>(b =>{
        b.HasKey(c => new { c.GuildId, c.Name });
        b.HasIndex(c => c.GuildId);
      });
    builder.Entity<GuildUser>(b => {
        b.HasKey(u => new { u.Id, u.GuildId });
      });
    builder.Entity<Username>(b => {
        b.HasKey(c => new { c.UserId, c.Date });
        b.HasIndex(c => c.UserId);
      });
    builder.Entity<Role>(b => {
        b.HasOne(r => r.Guild).WithMany(g => g.Roles);
      });
    builder.Entity<MinRole>(b => {
        b.HasKey(r => new { r.GuildId, r.Type });
        b.HasOne(r => r.Guild).WithMany(g => g.MinRoles);
      });
    builder.Entity<UserRole>(b => {
        b.HasKey(r => new { r.UserId, r.GuildId, r.RoleId });
        b.HasOne(r => r.User).WithMany(u => u.Roles).HasForeignKey(r => new { r.UserId, r.GuildId });
        b.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(r => r.RoleId);
      });
  //builder.Entity<CounterEvent>(b => {
        //b.HasKey(c => new { c.CounterId, c.Timestamp });
        //b.HasOne(c => c.Counter)
         //.WithMany(c => c.Events)
         //.HasForeignKey(c => c.CounterId);
        //b.HasOne(c => c.Channel)
         //.WithMany(c => c.Events)
         //.HasForeignKey(c => new { c.ChannelId, c.GuildId });
        //b.HasOne(c => c.GuildUser)
         //.WithMany(u => u.Events)
         //.HasForeignKey(c => new { c.GuildId, c.UserId });
     //});
    builder.Entity<SubredditChannel>(b => {
        b.HasKey(s => new { s.Name, s.ChannelId });
        b.HasOne(c => c.Subreddit)
          .WithMany(s => s.Channels)
          .HasForeignKey(s => s.Name);
        b.HasOne(c => c.Channel)
          .WithMany(s => s.Subreddits)
          .HasForeignKey(c => c.ChannelId);
      });
    builder.Entity<AbstractTempAction>(b => {
        b.HasIndex(t => t.Expiration).HasName("IX_temp_actions_Expiration");
        b.HasOne(a => a.User).WithMany(u => u.Actions).HasForeignKey(a => new { a.UserId, a.GuildId });
      });
  }

  public async Task Save() {
    if(!AllowSave)
      return;
    var changes = await SaveChangesAsync();
    if(changes > 0)
      Log.Info($"Saved {changes} changes to the database.");
  }

  public async Task<GuildUser>GetGuildUser(IGuildUser iuser) {
    var user = await GuildUsers.FindAsync(iuser.Id, iuser.Guild.Id);
    if(user == null) {
      user = GuildUsers.Add(new GuildUser(iuser) {
          User = await Users.Get(iuser)
        }).Entity;
    }
    return user;
  }

  public async Task<bool> RemoveGuildUser(IGuildUser iuser) {
    var user = await GuildUsers.FindAsync(iuser.Id, iuser.Guild.Id);
    if (user == null)
      return false;
    GuildUsers.Remove(user);
    return true;
  }

  public async Task<Subreddit> GetSubreddit(string name) {
    name = Subreddit.SanitizeName(name);
    var subreddit = await Subreddits.FindAsync(name);
    if(subreddit == null) {
      subreddit = Subreddits.Add(new Subreddit {
          Name = name,
          LastPost = DateTimeOffset.Now
        }).Entity;
    }
    if (subreddit.Channels != null)
      await Entry(subreddit).Collection(s => s.Channels).LoadAsync();
    return subreddit;
  }

  public async Task<UserRole> GetUserRole(IGuildUser user, IRole role) {
    var userRole = await UserRoles.FindAsync(user.Id, role.Id, user.Guild.Id);
    if (userRole == null)
      userRole = UserRoles.Add(new UserRole(user, role) {
            Role = await Roles.Get(role)
          }).Entity;
    return userRole;
  }

  public async Task RefreshUser(SocketGuildUser user) {
    Log.Info($"Refreshing {user.ToIDString()}");
    var guildUser = await GetGuildUser(user);
    await Entry(guildUser).Collection(u => u.Roles).LoadAsync();
    Log.Info($"Loaded {user.ToIDString()} roles");
    var roleIds = new HashSet<ulong>(user.Roles.Select(r => r.Id));
    await Task.WhenAll(user.Guild.Roles.Select(async role => {
      var userRole = await GetUserRole(user, role);
      userRole.HasRole = roleIds.Contains(role.Id);
    }));
    Log.Info($"Refreshed {user.ToIDString()}");
  }

  public async Task RefreshGuild(SocketGuild guild) {
    Log.Info($"Refreshing {guild.ToIDString()}");
    var dbGuild = await Guilds.Get(guild);
    await Entry(dbGuild).Collection(g => g.Channels).LoadAsync();
    await Entry(dbGuild).Collection(g => g.Roles).LoadAsync();
    Log.Info($"Loaded {guild.ToIDString()} entities.");
    var messageChannels = guild.Channels.OfType<IMessageChannel>();
    var channelIds = new HashSet<ulong>(messageChannels.Select(c => c.Id));
    var roleIds = new HashSet<ulong>(guild.Roles.Select(r => r.Id));
    foreach(var channel in messageChannels)
      await Channels.Get(channel);
    //var rTask = Task.WhenAll(guild.Roles.Where(r => r.Id != guild.EveryoneRole.Id)
        //.Select(r => Roles.Get(r)));
    //await Task.WhenAll(chTask);
    Log.Info($"Added new {guild.ToIDString()} entities.");
    Channels.RemoveRange(dbGuild.Channels.Where(c => !channelIds.Contains(c.Id)));
    Roles.RemoveRange(dbGuild.Roles.Where(r => !roleIds.Contains(r.Id)));
    //Log.Info($"Removed deleted {guild.ToIDString()} entities.");
    //if (!guild.HasAllMembers) {
      //Log.Info($"Downloading {guild.ToIDString()} users.");
      //await guild.DownloadUsersAsync();
      //Log.Info($"Downloaded {guild.ToIDString()} users.");
    //}
    //Log.Info($"Refreshing {guild.ToIDString()} users ({guild.Users.Count}).");
    //await Task.WhenAll(guild.Users.Select(user => {
      //if(user.Username == null) {
        //Log.Error($"Found user {user.Id} without a username");
        //return Task.CompletedTask;
      //}
      //return RefreshUser(user);
    //}));
    //Log.Info($"{guild.ToIDString()} refreshed.");
  }

}


}
