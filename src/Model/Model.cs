using Discord;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Hourai.Model {

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

  //// Analytics Data
  //public DbSet<Counter> Counters { get; set; }

  // Service Data
  public DbSet<Subreddit> Subreddits { get; set; }
  public DbSet<SubredditChannel> SubredditChannels { get; set; }

  public bool AllowSave { get; set; } = true;

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
    if (!Config.IsLoaded) {
      Config.Load();
      Log.Info($"Database File: {Config.DbFilename}");
    }
    optionsBuilder.UseSqlite($"Filename={Config.DbFilename}");
  }

  protected override void OnModelCreating(ModelBuilder builder) {
    builder.Entity<Guild>(b => {
        b.Property(g => g.Prefix)
          .HasDefaultValue("~");
      });
    builder.Entity<GuildUser>()
      .HasKey(u => new { u.Id, u.GuildId });
    builder.Entity<Channel>()
      .HasKey(c => new { c.Id, c.GuildId });
    builder.Entity<CustomCommand>(b =>{
        b.HasKey(c => new { c.GuildId, c.Name });
        b.HasIndex(c => c.GuildId);
      });
    builder.Entity<Username>(b => {
        b.HasKey(c => new { c.UserId, c.Date });
        b.HasIndex(c => c.UserId);
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
          .HasForeignKey(c => new { c.ChannelId, c.GuildId });
      });
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
      .SingleOrDefault(g => g.Id == iguild.Id);
  }


  public Guild GetGuild(IGuild iguild) {
    var guild = FindGuild(iguild);
    if(guild == null) {
      guild = new Guild(iguild);
      Guilds.Add(guild);
    }
    return guild;
  }

  public bool RemoveGuild(IGuild iguild) {
    var guild = FindGuild(iguild);
    if(guild == null)
      return false;
    Guilds.Remove(guild);
    return true;
  }

  public User FindUser(IUser iuser) {
    Check.NotNull(iuser);
    return Users.Include(u => u.Usernames).SingleOrDefault(u => u.Id == iuser.Id);
  }

  public User GetUser(IUser iuser) {
    var user = FindUser(iuser);
    if(user == null) {
      user = new User(iuser);
      Users.Add(user);
    }
    return user;
  }

  public bool RemoveUser(IUser iuser) {
    var user = FindUser(iuser);
    if(user == null)
      return false;
    Users.Remove(user);
    return true;
  }

  public GuildUser FindGuildUser(IGuildUser iuser) {
    Check.NotNull(iuser);
    return GuildUsers.SingleOrDefault(u =>
        (u.Id == iuser.Id) && (u.GuildId == iuser.Guild.Id));
  }

  public GuildUser GetGuildUser(IGuildUser iuser) {
    var user = FindGuildUser(iuser);
    if(user == null) {
      user = new GuildUser(iuser) {
        User = GetUser(iuser)
      };
      GuildUsers.Add(user);
    }
    return user;
  }

  public bool RemoveGuildUser(IGuildUser iuser) {
    var user = FindGuildUser(iuser);
    if(user == null)
      return false;
    GuildUsers.Remove(user);
    return true;
  }

  public Channel FindChannel(IGuildChannel ichannel) {
    Check.NotNull(ichannel);
    return Channels.Include(c => c.Subreddits)
      .SingleOrDefault(c =>
        (c.Id == ichannel.Id) && (c.GuildId == ichannel.Guild.Id));
  }

  public Channel GetChannel(IGuildChannel ichannel) {
    var channel = FindChannel(ichannel);
    if(channel == null) {
      channel = new Channel(ichannel);
      Channels.Add(channel);
      Entry(channel).Collection(s => s.Subreddits).Load();
    }
    return channel;
  }

  public bool RemoveChannel(IGuildChannel ichannel) {
    var channel = FindChannel(ichannel);
    if(channel == null)
      return false;
    Channels.Remove(channel);
    return true;
  }

  public Subreddit FindSubreddit(string name) {
    name = SanitizeSubredditName(name);
    return Subreddits.Include(s => s.Channels).SingleOrDefault(s => s.Name == name);
  }

  public async Task<Subreddit> GetSubreddit(string name) {
    var subreddit = FindSubreddit(name);
    if(subreddit == null) {
      subreddit = new Subreddit {
        Name = SanitizeSubredditName(name),
        LastPost = DateTimeOffset.Now
      };
      Subreddits.Add(subreddit);
      Entry(subreddit).Collection(s => s.Channels).Load();
      await Save();
    }
    return subreddit;
  }

  public string SanitizeSubredditName(string name) {
    return name.Trim().ToLower();
  }

}


}
