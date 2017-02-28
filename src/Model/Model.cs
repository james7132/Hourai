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

  public static T Get<T>(this DbSet<T> set, IEntity<ulong> entity) where T : class {
    var val = set.Find(entity.Id);
    if(val == null) {
      val = (T) Activator.CreateInstance(typeof(T), entity);
      set.Add(val);
    }
    return val;
  }

  public static bool Remove<T>(this DbSet<T> set, IEntity<ulong> entity) where T : class {
    var val = set.Find(entity.Id);
    if(val == null)
      return false;
    set.Remove(val);
    return true;
  }

  public static Channel Get(this DbSet<Channel> set, IChannel ichannel) {
    var channel = set.Find(ichannel.Id);
    if (channel == null)
      channel = set.Add(new Channel(ichannel) {
            GuildId = (channel as IGuildChannel)?.Id
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

  // Temporary Action Data
  public DbSet<AbstractTempAction> TempActions { get; set; }
  public DbSet<TempBan> TempBans { get; set; }
  public DbSet<TempRole> TempRoles { get; set; }

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
    builder.Entity<Guild>(b => {
        b.Property(g => g.Prefix).HasDefaultValue("~");
      });
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
    builder.Entity<AbstractTempAction>()
        .HasIndex(t => t.Expiration)
        .HasName("IX_temp_actions_Expiration");
  }

  public async Task Save() {
    if(!AllowSave)
      return;
    var changes = await SaveChangesAsync();
    if(changes > 0)
      Log.Info($"Saved {changes} changes to the database.");
  }

  public GuildUser GetGuildUser(IGuildUser iuser) {
    var user = GuildUsers.Find(iuser.Id, iuser.Guild.Id);
    if(user == null) {
      user = GuildUsers.Add(new GuildUser(iuser) {
          User = Users.Get(iuser)
        }).Entity;
    }
    if (user.Roles == null)
      Entry(user).Collection(s => s.Roles).Load();
    return user;
  }

  public bool RemoveGuildUser(IGuildUser iuser) {
    var user = GuildUsers.Find(iuser.Id, iuser.Guild.Id);
    if (user == null)
      return false;
    GuildUsers.Remove(user);
    return true;
  }

  public bool RemoveChannel(IGuildChannel ichannel) {
    var channel = Channels.Find(ichannel.Id);
    if(channel == null)
      return false;
    Channels.Remove(channel);
    return true;
  }

  public Subreddit GetSubreddit(string name) {
    name = Subreddit.SanitizeName(name);
    var subreddit = Subreddits.Find(name);
    if(subreddit == null) {
      subreddit = Subreddits.Add(new Subreddit {
          Name = name,
          LastPost = DateTimeOffset.Now
        }).Entity;
    }
    if (subreddit.Channels != null)
      Entry(subreddit).Collection(s => s.Channels).Load();
    return subreddit;
  }

  public UserRole GetUserRole(IGuildUser user, IRole role) {
    var userRole = UserRoles.Find(user.Id, role.Id, user.Guild.Id);
    if (userRole == null)
      userRole = UserRoles.Add(new UserRole(user, role) {
            Role = Roles.Get(role)
          }).Entity;
    return userRole;
  }

  public void RefreshUser(SocketGuildUser user) {
    var guildUser = GetGuildUser(user);
    //Entry(guild_user).Property(g => g.Guild).Load();
    Entry(guildUser.Guild).Collection(g => g.Roles).Load();
    var roles = guildUser.Guild.Roles.ToDictionary(r => r.Id, r => r);
    var roleIds = new HashSet<ulong>();
    foreach (var role in user.Roles) {
      var userRole = GetUserRole(user, role);
      guildUser.Roles.Add(userRole);
      roleIds.Add(role.Id);
    }
    foreach (var role in guildUser.Roles) {
      if (roleIds.Contains(role.RoleId))
        role.HasRole = true;
    }
  }

  public async Task RefreshGuild(SocketGuild guild) {
    var dbGuild = Guilds.Get(guild);
    var channelIds = new HashSet<ulong>();
    var roleIds = new HashSet<ulong>();
    Entry(dbGuild).Collection(g => g.Channels).Load();
    Entry(dbGuild).Collection(g => g.Roles).Load();
    foreach (var channel in guild.Channels.OfType<IMessageChannel>()) {
      Channels.Get(channel);
      channelIds.Add(channel.Id);
    }
    foreach (var role in guild.Roles) {
      if (role.Id == guild.EveryoneRole.Id)
        continue;
      Roles.Get(role);
      roleIds.Add(role.Id);
    }
    foreach (var channel in dbGuild.Channels) {
      if (!channelIds.Contains(channel.Id))
        Channels.Remove(channel);
    }
    foreach (var role in dbGuild.Roles) {
      if (!roleIds.Contains(role.Id))
        Roles.Remove(role);
    }
    if (!guild.HasAllMembers)
      await guild.DownloadUsersAsync();
    foreach(var user in guild.Users) {
      if(user.Username == null) {
        Log.Error($"Found user {user.Id} without a username");
        continue;
      }
      RefreshUser(user);
    }
  }

}


}
